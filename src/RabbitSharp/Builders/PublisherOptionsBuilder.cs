using RabbitSharp.MessageBus.Options;

namespace RabbitSharp.Builders
{
    public sealed class PublisherOptionsBuilder
    {
        private readonly PublisherOptions _config = new();

        public PublisherOptionsBuilder WithExchange(Action<ExchangeBuilder> configure)
        {
            var builder = new ExchangeBuilder(_config.Exchange);
            configure(builder);
            return this;
        }

        public PublisherOptionsBuilder WithRoutingKey(string routingKey)
        {
            _config.RoutingKey = routingKey;
            return this;
        }

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