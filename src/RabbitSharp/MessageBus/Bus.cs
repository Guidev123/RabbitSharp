using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using RabbitSharp.Abstractions;
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
            await PublishAsync(message, ExchangeTypeEnum.Topic, cancellationToken);
        }

        public async Task PublishAsync<T>(T message, ExchangeTypeEnum exchangeType, CancellationToken cancellationToken = default) where T : IMessage
        {
            await EnsureConnectedAsync();

            var exchangeName = _namingConventions.GetExchangeName<T>();
            var routingKey = _namingConventions.GetRoutingKey<T>(exchangeType);

            await _channel.ExchangeDeclareAsync(exchangeName, exchangeType.GetEnumDescription(), true, cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties
            {
                MessageId = message.CorrelationId.ToString(),
                Persistent = true,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>()
            };

            await _channel.BasicPublishAsync(exchangeName, routingKey, false, properties, body, cancellationToken);

            _logger.LogInformation("Published message {Message} with ID {CorrelationId} to exchange {ExchangeName}.",
                typeof(T).Name, message.CorrelationId, exchangeName);
        }

        public async Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessage, CancellationToken cancellationToken = default) where T : IMessage
        {
            await SubscribeAsync(queueName, onMessage, ExchangeTypeEnum.Topic, cancellationToken);
        }

        public async Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessage, ExchangeTypeEnum exchangeType, CancellationToken cancellationToken = default) where T : IMessage
        {
            await EnsureConnectedAsync();
            await _busFailureHandlingService.DeclareInfrastructureAsync<T>(queueName, _channel, exchangeType, cancellationToken);

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
                            correlationId, queueName);

                        await _channel.BasicAckAsync(deliveryTag, false);

                        return;
                    }

                    _logger.LogInformation("Received message {Message} with ID {CorrelationId} from queue {QueueName}",
                        typeof(T).Name, message.CorrelationId, queueName);

                    await onMessage(message);

                    await _channel.BasicAckAsync(deliveryTag, false);

                    _logger.LogInformation("Processed message {Message} with ID {CorrelationId} from queue {QueueName}",
                        typeof(T).Name, message.CorrelationId, queueName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message {CorrelationId} from queue {QueueName}",
                        correlationId, queueName);

                    var retryCount = _busFailureHandlingService.GetRetryCount(eventArgs.BasicProperties);

                    if (retryCount < _busResilienceOptions.MaxDeliveryRetryAttempts)
                    {
                        await _busFailureHandlingService.SendToRetryQueueAsync(
                            eventArgs,
                            queueName,
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
                            correlationId, queueName, _busResilienceOptions.MaxDeliveryRetryAttempts);

                        await _busFailureHandlingService.SendToDeadLetterQueueAsync(
                            eventArgs,
                            queueName,
                            correlationId,
                            _channel,
                            cancellationToken);

                        await _channel.BasicAckAsync(deliveryTag, false);
                    }
                }
            };

            await _channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: cancellationToken);
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