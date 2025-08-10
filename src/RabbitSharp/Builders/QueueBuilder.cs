using RabbitSharp.Enums;
using RabbitSharp.Extensions;
using RabbitSharp.MessageBus.Options;

namespace RabbitSharp.Builders
{
    /// <summary>
    /// Helps configure a queue.
    /// </summary>
    public sealed class QueueBuilder
    {
        private readonly QueueOptions _config;

        public QueueBuilder(QueueOptions config)
        {
            _config = config;
        }

        /// <summary>Sets the queue name.</summary>
        public QueueBuilder WithName(string name)
        {
            _config.QueueName = name;
            return this;
        }

        /// <summary>Marks the queue as durable.</summary>
        public QueueBuilder AsDurable(bool durable = true)
        {
            _config.Durable = durable;
            return this;
        }

        /// <summary>Marks the queue as exclusive.</summary>
        public QueueBuilder AsExclusive(bool exclusive = true)
        {
            _config.Exclusive = exclusive;
            return this;
        }

        /// <summary>Enables auto-delete for the queue.</summary>
        public QueueBuilder WithAutoDelete(bool autoDelete = true)
        {
            _config.AutoDelete = autoDelete;
            return this;
        }

        /// <summary>Sets the maximum queue length.</summary>
        public QueueBuilder WithMaxLength(int maxLength)
        {
            _config.Arguments["x-max-length"] = maxLength;
            return this;
        }

        /// <summary>Sets the message time-to-live.</summary>
        public QueueBuilder WithTTL(TimeSpan ttl)
        {
            _config.Arguments["x-message-ttl"] = (int)ttl.TotalMilliseconds;
            return this;
        }

        /// <summary>Sets the queue expiration time.</summary>
        public QueueBuilder WithExpiration(TimeSpan expiration)
        {
            _config.Arguments["x-expires"] = (int)expiration.TotalMilliseconds;
            return this;
        }

        /// <summary>Sets the maximum priority level.</summary>
        public QueueBuilder WithMaxPriority(int priority)
        {
            _config.Arguments["x-max-priority"] = priority;
            return this;
        }

        /// <summary>Sets the overflow behavior.</summary>
        public QueueBuilder WithOverflowBehavior(OverFlowStrategyEnum behavior = OverFlowStrategyEnum.RejectPublish)
        {
            _config.Arguments["x-overflow"] = behavior.GetEnumDescription();
            return this;
        }

        /// <summary>Enables or disables single active consumer.</summary>
        public QueueBuilder WithSingleActiveConsumer(bool enable = true)
        {
            if (enable)
                _config.Arguments["x-single-active-consumer"] = true;
            else
                _config.Arguments.Remove("x-single-active-consumer");
            return this;
        }

        /// <summary>Sets the dead-letter exchange and optional routing key.</summary>
        public QueueBuilder WithDeadLetterExchange(string exchange, string? routingKey = null)
        {
            _config.Arguments["x-dead-letter-exchange"] = exchange;

            if (!string.IsNullOrWhiteSpace(routingKey))
                _config.Arguments["x-dead-letter-routing-key"] = routingKey;

            return this;
        }

        /// <summary>Adds a custom queue argument.</summary>
        public QueueBuilder WithCustomArgument(string key, object? value)
        {
            _config.Arguments[key] = value;
            return this;
        }

        /// <summary>Marks the queue as lazy.</summary>
        public QueueBuilder AsLazyQueue()
        {
            _config.Arguments["x-queue-mode"] = "lazy";
            return this;
        }

        /// <summary>Configures the queue for high-volume processing.</summary>
        public QueueBuilder ForHighVolume()
        {
            return WithMaxLength(100000)
                   .WithOverflowBehavior(OverFlowStrategyEnum.DropHead)
                   .AsLazyQueue();
        }
    }
}