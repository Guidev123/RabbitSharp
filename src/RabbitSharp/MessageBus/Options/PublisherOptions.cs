namespace RabbitSharp.MessageBus.Options
{
    /// <summary>
    /// Represents configuration options for publishing messages to RabbitMQ.
    /// </summary>
    public sealed class PublisherOptions
    {
        /// <summary>
        /// Gets or sets the exchange configuration for publishing messages.
        /// </summary>
        public ExchangeOptions Exchange { get; set; } = new();

        /// <summary>
        /// Gets or sets the routing key to be used for message publishing.
        /// Can be <see langword="null"/> to use a default routing configuration.
        /// </summary>
        public string? RoutingKey { get; set; }
    }
}