using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitSharp.Abstractions;
using RabbitSharp.MessageBus;
using Simple;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

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

var host = builder.Build();
host.Run();