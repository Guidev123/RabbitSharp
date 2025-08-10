using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitSharp.Abstractions;
using RabbitSharp.Extensions;
using RabbitSharp.MessageBus.Options;

namespace RabbitSharp.MessageBus
{
    internal sealed class BusFailureHandlingService : IBusFailureHandlingService
    {
        private readonly ILogger<BusFailureHandlingService> _logger;
        private readonly INamingConventions _namingConventions;

        public BusFailureHandlingService(ILogger<BusFailureHandlingService> logger, INamingConventions namingConventions)
        {
            _logger = logger;
            _namingConventions = namingConventions;
        }

        public async Task DeclareInfrastructureAsync<T>(BusInfrastructureOptions options, IChannel channel, CancellationToken cancellationToken = default) where T : IMessage
        {
            if (string.IsNullOrEmpty(options.Exchange.ExchangeName))
            {
                options.Exchange.ExchangeName = _namingConventions.GetExchangeName<T>();
            }

            if (string.IsNullOrEmpty(options.RoutingKey))
            {
                options.RoutingKey = _namingConventions.GetRoutingKey<T>(options.Exchange.Type);
            }

            await Task.WhenAll(
                channel.ExchangeDeclareAsync(options.Exchange.ExchangeName, options.Exchange.Type.GetEnumDescription(), options.Exchange.Durable, arguments: options.Exchange.Arguments, cancellationToken: cancellationToken),
                DeclareQueueAsync(channel, options.MainQueue, cancellationToken),
                DeclareQueueAsync(channel, options.DeadLetterQueue, cancellationToken)
                );

            var retryConfig = options.RetryQueue;
            retryConfig.Arguments["x-dead-letter-exchange"] = options.Exchange.ExchangeName;
            retryConfig.Arguments["x-dead-letter-routing-key"] = options.RoutingKey;

            await Task.WhenAll(
                DeclareQueueAsync(channel, retryConfig, cancellationToken),
                channel.QueueBindAsync(options.MainQueue.QueueName, options.Exchange.ExchangeName, options.RoutingKey, cancellationToken: cancellationToken)
            );

            _logger.LogInformation("Infrastructure declared for {QueueName} with custom options", options.MainQueue.QueueName);
        }

        public async Task SendToRetryQueueAsync(
               BasicDeliverEventArgs eventArgs,
               string queueName,
               int retryCount,
               string correlationId,
               BusResilienceOptions busResilience,
               IChannel channel,
               CancellationToken cancellationToken)
        {
            var retryQueueName = $"{queueName}.retry";

            var baseDelayMs = busResilience.InitialDeliveryRetryDelay.TotalMilliseconds;
            var exponentialDelayMs = baseDelayMs * Math.Pow(2, retryCount - 1);

            var maxDelayMs = busResilience.MaxDeliveryRetryDelay.TotalMilliseconds;
            var delayMs = (int)Math.Min(exponentialDelayMs, maxDelayMs);

            var originalHeaders = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();
            var headers = new Dictionary<string, object?>(originalHeaders);

            var retryProperties = new BasicProperties
            {
                MessageId = eventArgs.BasicProperties.MessageId,
                Persistent = true,
                Timestamp = eventArgs.BasicProperties.Timestamp,
                Expiration = delayMs.ToString(),
                Headers = headers
            };

            retryProperties.Headers["x-retry-count"] = retryCount;

            await channel.BasicPublishAsync(string.Empty, retryQueueName, false, retryProperties, eventArgs.Body, cancellationToken);

            _logger.LogInformation("Message {CorrelationId} sent to retry queue {RetryQueueName} with delay {DelayMs}ms (max: {MaxDelayMs}ms). Retry count: {RetryCount}",
                correlationId, retryQueueName, delayMs, maxDelayMs, retryCount);
        }

        public async Task SendToDeadLetterQueueAsync(
            BasicDeliverEventArgs eventArgs,
            string queueName,
            string correlationId,
            IChannel channel,
            CancellationToken cancellationToken)
        {
            var deadLetterQueueName = $"{queueName}.deadletter";

            var originalHeaders = eventArgs.BasicProperties.Headers ?? new Dictionary<string, object?>();
            var headers = new Dictionary<string, object?>(originalHeaders);

            var dlqProperties = new BasicProperties
            {
                MessageId = eventArgs.BasicProperties.MessageId,
                Persistent = true,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                Headers = headers
            };

            dlqProperties.Headers["x-original-queue"] = queueName;
            dlqProperties.Headers["x-failed-at"] = DateTimeOffset.UtcNow.ToString("O");
            dlqProperties.Headers["x-final-retry-count"] = GetRetryCount(eventArgs.BasicProperties);

            await channel.BasicPublishAsync(string.Empty, deadLetterQueueName, false, dlqProperties, eventArgs.Body, cancellationToken);

            _logger.LogError("Message {CorrelationId} sent to dead letter queue {DeadLetterQueueName} after {RetryCount} failed attempts",
                correlationId, deadLetterQueueName, GetRetryCount(eventArgs.BasicProperties));
        }

        public int GetRetryCount(IReadOnlyBasicProperties? properties)
        {
            if (properties?.Headers?.TryGetValue("x-retry-count", out var retryCountObj) == true)
            {
                return retryCountObj switch
                {
                    int count => count,
                    byte[] bytes => BitConverter.ToInt32(bytes, 0),
                    _ => 0
                };
            }
            return 0;
        }

        private static async Task DeclareQueueAsync(IChannel channel, QueueOptions options, CancellationToken cancellationToken)
        {
            await channel.QueueDeclareAsync(
                options.QueueName,
                options.Durable,
                options.Exclusive,
                options.AutoDelete,
                options.Arguments,
                cancellationToken: cancellationToken);
        }
    }
}