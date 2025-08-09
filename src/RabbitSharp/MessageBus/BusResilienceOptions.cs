namespace RabbitSharp.MessageBus
{
    /// <summary>
    /// Defines resilience options for message delivery, including retry behavior and delay settings.
    /// </summary>
    public sealed class BusResilienceOptions
    {
        /// <summary>
        /// The maximum number of retry attempts allowed for message delivery before moving the message to the dead-letter queue.
        /// Default is <c>3</c>.
        /// </summary>
        public int MaxDeliveryRetryAttempts { get; set; } = 3;

        /// <summary>
        /// The initial delay applied before retrying a failed message delivery.
        /// Default is <c>1 second</c>.
        /// </summary>
        public TimeSpan InitialDeliveryRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// The maximum delay allowed between retry attempts for a failed message delivery.
        /// Default is <c>5 minutes</c>.
        /// </summary>
        public TimeSpan MaxDeliveryRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    }
}