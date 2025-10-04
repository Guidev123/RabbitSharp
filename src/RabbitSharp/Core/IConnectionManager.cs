using RabbitMQ.Client;

namespace RabbitSharp.Core
{
    internal interface IConnectionManager : IDisposable
    {
        Task EnsureConnectedAsync(CancellationToken cancellationToken = default);

        IConnection Connection { get; }

        IChannel Channel { get; }

        bool IsConnected { get; }
    }
}