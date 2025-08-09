using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitSharp.Abstractions;
using RabbitSharp.MessageBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.TryAddSingleton<IBusFailureHandlingService, BusFailureHandlingService>();

builder.Services.AddSingleton<INamingConventions, SimpleNamingConventions>();
builder.Services.TryAddSingleton<IBus>(serviceProvider =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Bus>>();
    var busFailureHandlingService = serviceProvider.GetRequiredService<IBusFailureHandlingService>();
    var brokerOptions = builder.Configuration.GetSection(nameof(BrokerOptions)).Get<BrokerOptions>() ?? new();
    var namingConventions = serviceProvider.GetRequiredService<INamingConventions>();

    return new Bus(options =>
    {
        options.Username = brokerOptions.Username;
        options.Password = brokerOptions.Password;
        options.VirtualHost = brokerOptions.VirtualHost;
        options.Hosts = brokerOptions.Hosts;
    }, busFailureHandlingService, logger, namingConventions);
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("api", async (IBus bus) =>
{
    await bus.PublishAsync(new SimpleMessage(), ExchangeTypeEnum.Topic);
    return Results.Ok("Message published successfully.");
});

app.Run();

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