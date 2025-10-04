﻿using RabbitSharp.Options;

namespace RabbitSharp.Abstractions
{
    /// <summary>
    /// Defines a generic message bus abstraction for publishing and subscribing to RabbitMQ messages.
    /// Supports flexible configuration of exchanges, queues, and routing options.
    /// </summary>
    public interface IBus : IDisposable
    {
        /// <summary>
        /// Publishes a message to the bus using the default configuration.
        /// </summary>
        /// <typeparam name="T">
        /// The message type. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="message">
        /// The message instance to publish. Cannot be <see langword="null"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token to abort the operation.
        /// </param>
        Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
            where T : IMessage;

        /// <summary>
        /// Publishes a message with custom publisher settings, including exchange, routing, and delivery options.
        /// </summary>
        /// <typeparam name="T">
        /// The message type. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="options">
        /// Publisher configuration, such as exchange type, routing key, and durability.
        /// </param>
        /// <param name="message">
        /// The message instance to publish. Cannot be <see langword="null"/>.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token to abort the operation.
        /// </param>
        Task PublishAsync<T>(PublisherOptions options, T message, CancellationToken cancellationToken = default)
             where T : IMessage;

        /// <summary>
        /// Subscribes to a queue and processes messages of type <typeparamref name="T"/> using a specified handler.
        /// </summary>
        /// <typeparam name="T">
        /// The message type. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="queueName">
        /// The target queue name.
        /// </param>
        /// <param name="onMessage">
        /// Asynchronous callback invoked when a message is received.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token to stop the subscription.
        /// </param>
        Task SubscribeAsync<T>(string queueName, Func<T, Task> onMessage, CancellationToken cancellationToken = default)
            where T : IMessage;

        /// <summary>
        /// Subscribes to a queue with full infrastructure configuration (exchange, bindings, DLQ, retry, etc.)
        /// and processes messages of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">
        /// The message type. Must implement <see cref="IMessage"/>.
        /// </typeparam>
        /// <param name="options">
        /// Complete bus infrastructure configuration for this subscription.
        /// </param>
        /// <param name="onMessage">
        /// Asynchronous callback invoked when a message is received.
        /// </param>
        /// <param name="cancellationToken">
        /// Optional cancellation token to stop the subscription.
        /// </param>
        Task SubscribeAsync<T>(BusInfrastructureOptions options, Func<T, Task> onMessage, CancellationToken cancellationToken = default)
           where T : IMessage;
    }
}