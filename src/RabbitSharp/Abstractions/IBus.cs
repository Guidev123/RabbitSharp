using RabbitSharp.MessageBus.Options;

namespace RabbitSharp.Abstractions
{
    /// <summary>
    /// Defines a generic messaging bus interface for publishing and subscribing to RabbitMQ messages.
    /// </summary>
    public interface IBus : IDisposable
    {
        /// <summary>
        /// Publishes a message to the bus.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message to publish. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="message">
        /// The message instance to publish. Cannot be <see langword="null"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the task to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous publish operation.
        /// </returns>
        Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
            where T : IMessage;

        /// <summary>
        /// Publishes a message to the bus using a specific exchange type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message to publish. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="message">
        /// The message instance to publish. Cannot be <see langword="null"/>.
        /// </param>
        /// <param name="exchangeType">
        /// The type of exchange to use for publishing.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the task to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous publish operation.
        /// </returns>
        Task PublishAsync<T>(PublisherOptions options, T message, CancellationToken cancellationToken = default)
             where T : IMessage;

        /// <summary>
        /// Subscribes to a queue and registers a handler for processing messages of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message to subscribe to. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="queueName">
        /// The name of the queue to subscribe to.
        /// </param>
        /// <param name="onMessage">
        /// The asynchronous handler invoked when a message is received.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the subscription to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous subscription operation.
        /// </returns>
        Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessage, CancellationToken cancellationToken = default)
            where T : IMessage;

        /// <summary>
        /// Subscribes to a queue and registers a handler for processing messages of type <typeparamref name="T"/> using a specific exchange type.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message to subscribe to. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="queueName">
        /// The name of the queue to subscribe to.
        /// </param>
        /// <param name="onMessage">
        /// The asynchronous handler invoked when a message is received.
        /// </param>
        /// <param name="exchangeType">
        /// The type of exchange to bind the subscription to.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the subscription to complete. Defaults to <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous subscription operation.
        /// </returns>
        Task SubscribeAsync<T>(BusInfrastructureOptions options, Func<T, Task> onMessage, CancellationToken cancellationToken = default)
           where T : IMessage;
    }
}