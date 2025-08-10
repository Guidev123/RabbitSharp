namespace RabbitSharp.MessageBus.Options
{
    /// <summary>
    /// Defines connection settings for the RabbitMQ broker.
    /// </summary>
    public sealed class BrokerOptions
    {
        /// <summary>
        /// The maximum number of attempts to reconnect to the broker if the initial connection fails.
        /// Default is <c>5</c>.
        /// </summary>
        public int TryConnectMaxRetries { get; set; } = 5;

        /// <summary>
        /// The interval, in seconds, between reconnection attempts.
        /// Default is <c>10 seconds</c>.
        /// </summary>
        public int NetworkRecoveryIntervalInSeconds { get; set; } = 10;

        /// <summary>
        /// The heartbeat interval, in seconds, used to detect and maintain the connection's health.
        /// Default is <c>60 seconds</c>.
        /// </summary>
        public int HeartbeatIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// The list of RabbitMQ hostnames or IP addresses to connect to.
        /// </summary>
        public string[] Hosts { get; set; } = null!;

        /// <summary>
        /// The username used for authentication with the broker.
        /// </summary>
        public string Username { get; set; } = null!;

        /// <summary>
        /// The password used for authentication with the broker.
        /// </summary>
        public string Password { get; set; } = null!;

        /// <summary>
        /// The RabbitMQ virtual host to connect to.
        /// Default is <c>"/"</c>.
        /// </summary>
        public string VirtualHost { get; set; } = "/";
    }
}