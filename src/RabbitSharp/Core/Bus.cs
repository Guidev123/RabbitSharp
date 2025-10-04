using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitSharp.Abstractions;
using RabbitSharp.Builders;
using RabbitSharp.Enums;
using RabbitSharp.Extensions;
using RabbitSharp.Options;
using System.Text.Json;

namespace RabbitSharp.Core
{
    internal sealed class Bus : IBus
    {
        private readonly IBusFailureHandlingService _busFailureHandlingService;
        private readonly IConnectionManager _connectionManager;
        private readonly INamingConventions _namingConventions;
        private readonly ILogger<Bus> _logger;
        private readonly BusResilienceOptions _busResilienceOptions = new();

        public Bus(IBusFailureHandlingService busFailureHandlingService,
                   IConnectionManager connectionManager,
                   ILogger<Bus> logger,
                   INamingConventions namingConventions,
                   Action<BusResilienceOptions>? configureResilience = null)
        {
            _busResilienceOptions = new BusResilienceOptions();
            configureResilience?.Invoke(_busResilienceOptions);
            _busFailureHandlingService = busFailureHandlingService;
            _logger = logger;
            _namingConventions = namingConventions;
            _connectionManager = connectionManager;
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
            await _connectionManager.EnsureConnectedAsync(cancellationToken);

            var channel = _connectionManager.Channel;

            await channel.ExchangeDeclareAsync(options.Exchange.ExchangeName!, options.Exchange.Type.GetEnumDescription(), true, arguments: options.Exchange.Arguments, cancellationToken: cancellationToken);

            var body = JsonSerializer.SerializeToUtf8Bytes(message);
            var properties = new BasicProperties
            {
                MessageId = message.CorrelationId.ToString(),
                Persistent = true,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object?>()
            };

            await channel.BasicPublishAsync(options.Exchange.ExchangeName!, options.RoutingKey!, false, properties, body, cancellationToken);

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
            await _connectionManager.EnsureConnectedAsync(cancellationToken);

            var channel = _connectionManager.Channel;

            await _busFailureHandlingService.DeclareInfrastructureAsync<T>(options, channel, cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
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

                        await channel.BasicAckAsync(deliveryTag, false);
                        return;
                    }

                    _logger.LogInformation("Received message {Message} with ID {CorrelationId} from queue {QueueName}",
                        typeof(T).Name, message.CorrelationId, options.MainQueue.QueueName);

                    await onMessage(message);

                    await channel.BasicAckAsync(deliveryTag, false);

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
                            channel,
                            cancellationToken);

                        await channel.BasicAckAsync(deliveryTag, false);

                        _logger.LogWarning(
                            "Message {CorrelationId} sent to retry queue. Attempt {RetryCount}/{MaxDeliveryRetryAttempts}",
                            correlationId, retryCount + 1, _busResilienceOptions.MaxDeliveryRetryAttempts);
                    }
                    else
                    {
                        _logger.LogError(
                            "Message {CorrelationId} from queue {QueueName} exceeded maximum retries ({MaxDeliveryRetryAttempts})",
                            correlationId, options.MainQueue.QueueName, _busResilienceOptions.MaxDeliveryRetryAttempts);

                        await _busFailureHandlingService.SendToDeadLetterQueueAsync(
                            eventArgs,
                            options.DeadLetterQueue.QueueName,
                            correlationId,
                            channel,
                            cancellationToken);

                        await channel.BasicAckAsync(deliveryTag, false);
                    }
                }
            };

            await channel.BasicConsumeAsync(options.MainQueue.QueueName, false, consumer, cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            _connectionManager?.Dispose();
        }
    }
}