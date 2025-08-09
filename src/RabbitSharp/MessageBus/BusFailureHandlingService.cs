using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitSharp.Abstractions;

namespace RabbitSharp.MessageBus
{
    public sealed class BusFailureHandlingService : IBusFailureHandlingService
    {
        private readonly ILogger<BusFailureHandlingService> _logger;
        private readonly INamingConventions _namingConventions;

        public BusFailureHandlingService(ILogger<BusFailureHandlingService> logger, INamingConventions namingConventions)
        {
            _logger = logger;
            _namingConventions = namingConventions;
        }

        public async Task DeclareInfrastructureAsync<T>(string queueName, IChannel channel, CancellationToken cancellationToken = default) where T : IMessage
        {
            await DeclareInfrastructureAsync<T>(queueName, channel, ExchangeTypeEnum.Topic, cancellationToken);
        }

        public async Task DeclareInfrastructureAsync<T>(string queueName, IChannel channel, ExchangeTypeEnum exchangeType, CancellationToken cancellationToken = default) where T : IMessage
        {
            var exchangeName = _namingConventions.GetExchangeName<T>();
            var routingKey = _namingConventions.GetRoutingKey<T>(exchangeType);
            var retryQueueName = $"{queueName}.retry";
            var deadLetterQueueName = $"{queueName}.deadletter";

            await Task.WhenAll(
                channel.ExchangeDeclareAsync(exchangeName, exchangeType.GetEnumDescription(), true, cancellationToken: cancellationToken),
                channel.QueueDeclareAsync(queueName, true, false, false, cancellationToken: cancellationToken),
                channel.QueueDeclareAsync(deadLetterQueueName, true, false, false, cancellationToken: cancellationToken)
                );

            var retryQueueArgs = new Dictionary<string, object?>
            {
                {"x-dead-letter-exchange", exchangeName},
                {"x-dead-letter-routing-key", routingKey}
            };

            await Task.WhenAll(
                channel.QueueDeclareAsync(retryQueueName, true, false, false, retryQueueArgs, cancellationToken: cancellationToken),
                channel.QueueBindAsync(queueName, exchangeName, routingKey, cancellationToken: cancellationToken)
                );
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

            var originalHeaders = eventArgs.BasicProperties.Headers
                ?? throw new ArgumentNullException(nameof(queueName), "Message headers are required for retry processing.");

            var headers = new Dictionary<string, object?>(originalHeaders);

            var retryProperties = new BasicProperties
            {
                MessageId = eventArgs.BasicProperties.MessageId,
                Persistent = true,
                Timestamp = eventArgs.BasicProperties.Timestamp,
                Expiration = delayMs.ToString(),
                Headers = headers!
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

            var originalHeaders = eventArgs.BasicProperties.Headers
                ?? throw new ArgumentNullException(nameof(queueName), "Message headers are required for dead letter processing.");

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
    }
}