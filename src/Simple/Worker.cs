using RabbitSharp.Abstractions;

namespace Simple
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IBus>();

            SetSubscribers(bus, stoppingToken);

            return Task.CompletedTask;
        }

        private static void SetSubscribers(IBus bus, CancellationToken stoppingToken)
        {
            bus.SubscribeAsync<SimpleMessage>("example.simplemessage", async (simpleMessage) =>
            {
                await Task.Delay(1000, stoppingToken);

                //throw new Exception("Simulated processing failure.");
            }, ExchangeTypeEnum.Topic, stoppingToken);
        }
    }

    public sealed record SimpleMessage : IMessage
    {
        public SimpleMessage()
        {
            CorrelationId = Guid.NewGuid();
        }

        public SimpleMessage(Guid correlationId)
        {
            CorrelationId = correlationId;
        }

        public Guid CorrelationId { get; init; }
    }
}