using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitSharp.Abstractions;
using RabbitSharp.Builders;
using RabbitSharp.Enums;
using RabbitSharp.Extensions;
using RabbitSharp.MessageBus.Options;
using System.Net.Sockets;
using System.Text.Json;

namespace RabbitSharp.MessageBus
{
    internal sealed class Bus : IBus
    {
        private readonly BrokerOptions _brokerOptions = new();
        private readonly BusResilienceOptions _busResilienceOptions = new();

        private readonly IBusFailureHandlingService _busFailureHandlingService;
        private readonly ILogger<Bus> _logger;
        private readonly INamingConventions _namingConventions;

        private readonly ConnectionFactory _connectionFactory;
        private IConnection _connection = default!;
        private IChannel _channel = default!;

        public Bus(Action<BrokerOptions> configureBroker,
                   IBusFailureHandlingService busFailureHandlingService,
                   ILogger<Bus> logger,
                   INamingConventions namingConventions,
                   Action<BusResilienceOptions>? configureResilience = null)
        {
            configureBroker(_brokerOptions);
            if (configureResilience is not null)
            {
                configureResilience(_busResilienceOptions);
            }

            _connectionFactory = new ConnectionFactory()
            {
                UserName = _brokerOptions.Username,
                Password = _brokerOptions.Password,
                VirtualHost = _brokerOptions.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(_brokerOptions.NetworkRecoveryIntervalInSeconds),
                RequestedHeartbeat = TimeSpan.FromSeconds(_brokerOptions.HeartbeatIntervalSeconds),
            };

            _busFailureHandlingService = busFailureHandlingService;
            _logger = logger;
            _namingConventions = namingConventions;
        }

        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : IMessage
        {
            var options = new PublisherOptionsBuilder()
                .WithExchange(exchange =>
                {
                    exchange.WithName(_namingConventions.GetExchangeName<T>());
                    exchange.WithType(ExchangeTypeEnum.Topic);
                })
                .WithRoutingKey(_namingConventions.GetRoutingKey<T>(ExchangeTypeEnum.Topic))
                .Build();

            await PublishAsync(options, message, cancellationToken);
        }

        public async Task PublishAsync<T>(PublisherOptions options, T message, CancellationToken cancellationToken = default) where T : IMessage
        {
            await EnsureConnectedAsync();

            await _channel.ExchangeDeclareAsync(options.Exchange.ExchangeName!, options.Exchange.Type.GetEnumDescription(), true, arguments: options.Exchange.Arguments, cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties
            {
                MessageId = message.CorrelationId.ToString(),
                Persistent = true,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>()
            };

            await _channel.BasicPublishAsync(options.Exchange.ExchangeName!, options.RoutingKey!, false, properties, body, cancellationToken);

            _logger.LogInformation("Published message {Message} with ID {CorrelationId} to exchange {ExchangeName}.",
                typeof(T).Name, message.CorrelationId, options.Exchange.ExchangeName);
        }

        public async Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessage, CancellationToken cancellationToken = default) where T : IMessage
        {
            var options = new BusInfrastructureBuilder(queueName)
                .WithExchange(exchange =>
                {
                    exchange.WithType(ExchangeTypeEnum.Topic);
                    exchange.WithName(_namingConventions.GetExchangeName<T>());
                })
                .WithRoutingKey(_namingConventions.GetRoutingKey<T>(ExchangeTypeEnum.Topic))
                .Build();

            await SubscribeAsync(options, onMessage, cancellationToken);
        }

        public async Task SubscribeAsync<T>(BusInfrastructureOptions options, Func<T, Task> onMessage, CancellationToken cancellationToken = default) where T : IMessage
        {
            await EnsureConnectedAsync();
            await _busFailureHandlingService.DeclareInfrastructureAsync<T>(options, _channel, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, eventArgs) =>
            {
                var deliveryTag = eventArgs.DeliveryTag;
                var correlationId = eventArgs.BasicProperties?.MessageId ?? string.Empty;

                try
                {
                    var body = eventArgs.Body.ToArray();
                    var message = JsonSerializer.Deserialize<T>(body);

                    if (message is null)
                    {
                        _logger.LogError("Failed to deserialize message {CorrelationId} from queue {QueueName}",
                            correlationId, options.MainQueue.QueueName);

                        await _channel.BasicAckAsync(deliveryTag, false);

                        return;
                    }

                    _logger.LogInformation("Received message {Message} with ID {CorrelationId} from queue {QueueName}",
                        typeof(T).Name, message.CorrelationId, options.MainQueue.QueueName);

                    await onMessage(message);

                    await _channel.BasicAckAsync(deliveryTag, false);

                    _logger.LogInformation("Processed message {Message} with ID {CorrelationId} from queue {QueueName}",
                        typeof(T).Name, message.CorrelationId, options.MainQueue.QueueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {CorrelationId} from queue {QueueName}",
                        correlationId, options.MainQueue.QueueName);

                    var retryCount = _busFailureHandlingService.GetRetryCount(eventArgs.BasicProperties);

                    if (retryCount < _busResilienceOptions.MaxDeliveryRetryAttempts)
                    {
                        await _busFailureHandlingService.SendToRetryQueueAsync(
                            eventArgs,
                            options.RetryQueue.QueueName,
                            retryCount + 1,
                            correlationId,
                            _busResilienceOptions,
                            _channel, cancellationToken);

                        await _channel.BasicAckAsync(deliveryTag, false);

                        _logger.LogWarning("Message {CorrelationId} sent to retry queue. Attempt {RetryCount}/{MaxDeliveryRetryAttempts}",
                            correlationId, retryCount + 1, _busResilienceOptions.MaxDeliveryRetryAttempts);
                    }
                    else
                    {
                        _logger.LogError("Message {CorrelationId} from queue {QueueName} exceeded maximum retries ({MaxDeliveryRetryAttempts})",
                            correlationId, options.MainQueue.QueueName, _busResilienceOptions.MaxDeliveryRetryAttempts);

                        await _busFailureHandlingService.SendToDeadLetterQueueAsync(
                            eventArgs,
                            options.DeadLetterQueue.QueueName,
                            correlationId,
                            _channel,
                            cancellationToken);

                        await _channel.BasicAckAsync(deliveryTag, false);
                    }
                }
            };

            await _channel.BasicConsumeAsync(options.MainQueue.QueueName, false, consumer, cancellationToken: cancellationToken);
        }

        private async Task TryConnect()
        {
            var policy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<IOException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(_brokerOptions.TryConnectMaxRetries, retry =>
                {
                    _logger.LogWarning("Attempting to connect to RabbitMQ. Retry {RetryCount}/{TryConnectMaxRetries}",
                        retry, _brokerOptions.TryConnectMaxRetries);

                    return TimeSpan.FromSeconds(Math.Pow(2, retry));
                });

            await policy.ExecuteAsync(async () =>
            {
                var endpoints = _brokerOptions.Hosts.Select(host => new AmqpTcpEndpoint(host)).ToList();

                _connection = await _connectionFactory.CreateConnectionAsync(endpoints);
                _channel = await _connection.CreateChannelAsync();
                _logger.LogInformation("Successfully connected to RabbitMQ");
            });
        }

        private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);

        private async Task EnsureConnectedAsync()
        {
            if (!IsConnected)
            {
                await _connectionSemaphore.WaitAsync();
                try
                {
                    if (!IsConnected) await TryConnect();
                }
                finally
                {
                    _connectionSemaphore.Release();
                }
            }
        }

        private bool IsConnected
            => _connection?.IsOpen == true && _channel?.IsOpen == true && !_channel.IsClosed;

        public void Dispose()
        {
            _connectionSemaphore?.Dispose();

            try { _channel?.CloseAsync(); } catch { }
            try { _connection?.CloseAsync(); } catch { }

            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}