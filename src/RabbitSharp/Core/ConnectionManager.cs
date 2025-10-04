using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitSharp.Options;
using System.Net.Sockets;

namespace RabbitSharp.Core
{
    internal sealed class ConnectionManager : IConnectionManager
    {
        private readonly BrokerOptions _brokerOptions;
        private readonly ILogger<ConnectionManager> _logger;
        private readonly ConnectionFactory _connectionFactory;
        private readonly SemaphoreSlim _connectionSemaphore;
        private IConnection? _connection;
        private IChannel? _channel;

        public ConnectionManager(
            BrokerOptions brokerOptions,
            ILogger<ConnectionManager> logger)
        {
            _brokerOptions = brokerOptions;
            _logger = logger;
            _connectionFactory = new ConnectionFactory
            {
                UserName = brokerOptions.Username,
                Password = brokerOptions.Password,
                VirtualHost = brokerOptions.VirtualHost,
                RequestedHeartbeat = TimeSpan.FromSeconds(brokerOptions.HeartbeatIntervalSeconds),
                NetworkRecoveryInterval = TimeSpan.FromSeconds(brokerOptions.NetworkRecoveryIntervalInSeconds),
                AutomaticRecoveryEnabled = true
            };

            _connectionSemaphore = new SemaphoreSlim(1, 1);
        }

        public IConnection Connection => _connection ?? throw new NotImplementedException();

        public IChannel Channel => _channel ?? throw new NotImplementedException();

        public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true && !_channel.IsClosed;

        public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (IsConnected is false)
            {
                await _connectionSemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (IsConnected is false)
                    {
                        await TryConnectAsync(cancellationToken);
                    }
                }
                finally
                {
                    _connectionSemaphore.Release();
                }
            }
        }

        private async Task TryConnectAsync(CancellationToken cancellationToken)
        {
            var policy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<IOException>()
                .Or<SocketException>()
                .WaitAndRetryAsync(_brokerOptions.TryConnectMaxRetries, retry =>
                {
                    _logger.LogWarning("Attempting to connect to RabbitMQ. Retry {RetryCount}/{TryConnectMaxRetries}",
                        retry, _brokerOptions.TryConnectMaxRetries);

                    return TimeSpan.FromSeconds(Math.Pow(2, retry));
                });

            await policy.ExecuteAsync(async () =>
            {
                var endpoints = _brokerOptions.Hosts.Select(host => new AmqpTcpEndpoint(host)).ToList();

                _connection = await _connectionFactory.CreateConnectionAsync(endpoints);
                _channel = await _connection.CreateChannelAsync();

                await _channel.BasicQosAsync(0, _brokerOptions.PrefetchCount, false);

                _logger.LogInformation("Successfully connected to RabbitMQ");
            });
        }

        public void Dispose()
        {
            _connectionSemaphore?.Dispose();

            try { _channel?.CloseAsync(); } catch { }
            try { _connection?.CloseAsync(); } catch { }

            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}