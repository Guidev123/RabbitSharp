using RabbitSharp.Options;

namespace RabbitSharp.Builders
{
    /// <summary>
    /// Provides a fluent API for configuring and building <see cref="PublisherOptions"/> instances.
    /// </summary>
    public sealed class PublisherOptionsBuilder
    {
        private readonly PublisherOptions _config = new();

        /// <summary>
        /// Configures the exchange settings for the publisher.
        /// </summary>
        /// <param name="configure">
        /// An action that receives an <see cref="ExchangeBuilder"/> to define the exchange configuration.
        /// </param>
        /// <returns>
        /// The current <see cref="PublisherOptionsBuilder"/> instance for method chaining.
        /// </returns>
        public PublisherOptionsBuilder WithExchange(Action<ExchangeBuilder> configure)
        {
            var builder = new ExchangeBuilder(_config.Exchange);
            configure(builder);
            return this;
        }

        /// <summary>
        /// Sets the routing key to be used when publishing messages.
        /// </summary>
        /// <param name="routingKey">
        /// The routing key that determines how messages are routed by the exchange.
        /// </param>
        /// <returns>
        /// The current <see cref="PublisherOptionsBuilder"/> instance for method chaining.
        /// </returns>
        public PublisherOptionsBuilder WithRoutingKey(string routingKey)
        {
            _config.RoutingKey = routingKey;
            return this;
        }

        /// <summary>
        /// Builds and returns the configured <see cref="PublisherOptions"/> instance.
        /// </summary>
        /// <remarks>
        /// This method will throw an <see cref="InvalidOperationException"/> if a routing key is not specified.
        /// </remarks>
        /// <returns>
        /// A fully configured <see cref="PublisherOptions"/> instance.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no routing key has been set before calling this method.
        /// </exception>
        public PublisherOptions Build()
        {
            if (string.IsNullOrEmpty(_config.RoutingKey))
            {
                throw new InvalidOperationException("Routing key must be set before building the options.");
            }

            return _config;
        }
    }
}