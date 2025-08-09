using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitSharp.Abstractions;

namespace RabbitSharp.MessageBus
{
    /// <summary>
    /// Defines methods for handling message bus failures,
    /// including retry and dead-letter queue operations.
    /// </summary>
    public interface IBusFailureHandlingService
    {
        /// <summary>
        /// Declares the necessary retry and dead-letter infrastructure
        /// for a specific message type in the given queue.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message for which to declare infrastructure. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="queueName">
        /// The name of the queue where the message will be processed.
        /// </param>
        /// <param name="channel">
        /// The channel used to declare the infrastructure.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the operation to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        Task DeclareInfrastructureAsync<T>(string queueName, IChannel channel, CancellationToken cancellationToken = default)
            where T : IMessage;

        /// <summary>
        /// Declares the necessary retry and dead-letter infrastructure
        /// for a specific message type in the given queue, using a specific exchange type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message for which to declare infrastructure. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="queueName">
        /// The name of the queue where the message will be processed.
        /// </param>
        /// <param name="channel">
        /// The channel used to declare the infrastructure.
        /// </param>
        /// <param name="exchangeType">
        /// The exchange type to bind the queue to.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the operation to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        Task DeclareInfrastructureAsync<T>(string queueName, IChannel channel, ExchangeTypeEnum exchangeType, CancellationToken cancellationToken = default)
            where T : IMessage;

        /// <summary>
        /// Retrieves the number of retries a message has attempted based on its properties.
        /// </summary>
        /// <param name="properties">
        /// The message properties containing retry count metadata.
        /// </param>
        /// <returns>
        /// The number of retries, or <c>0</c> if none are found.
        /// </returns>
        int GetRetryCount(IReadOnlyBasicProperties? properties);

        /// <summary>
        /// Sends the message to the retry queue for reprocessing.
        /// </summary>
        /// <param name="eventArgs">
        /// The original message delivery event arguments.
        /// </param>
        /// <param name="queueName">
        /// The base queue name associated with the message.
        /// </param>
        /// <param name="retryCount">
        /// The number of times the message has been retried.
        /// </param>
        /// <param name="correlationId">
        /// The correlation identifier for tracking the message.
        /// </param>
        /// <param name="busResilience">
        /// The resilience configuration settings for the bus.
        /// </param>
        /// <param name="channel">
        /// The channel used to publish the message to the retry queue.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the operation to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        Task SendToRetryQueueAsync(
            BasicDeliverEventArgs eventArgs,
            string queueName,
            int retryCount,
            string correlationId,
            BusResilienceOptions busResilience,
            IChannel channel,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends the message to the dead-letter queue for storage and inspection.
        /// </summary>
        /// <param name="eventArgs">
        /// The original message delivery event arguments.
        /// </param>
        /// <param name="queueName">
        /// The base queue name associated with the message.
        /// </param>
        /// <param name="correlationId">
        /// The correlation identifier for tracking the message.
        /// </param>
        /// <param name="channel">
        /// The channel used to publish the message to the dead-letter queue.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the operation to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        Task SendToDeadLetterQueueAsync(
            BasicDeliverEventArgs eventArgs,
            string queueName,
            string correlationId,
            IChannel channel,
            CancellationToken cancellationToken);
    }
}