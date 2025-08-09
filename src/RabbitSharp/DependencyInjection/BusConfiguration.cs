using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RabbitSharp.Abstractions;
using RabbitSharp.MessageBus;

namespace RabbitSharp.DependencyInjection
{
    public static class BusConfiguration
    {
        public static IServiceCollection AddRabbitSharp(
            this IServiceCollection services,
            Action<BrokerOptions> brokerOptions,
            Action<BusResilienceOptions>? busResilienceOptions = null)
        {
            services.TryAddSingleton<IBusFailureHandlingService, BusFailureHandlingService>();
            services.AddSingleton<INamingConventions, SimpleNamingConventions>();
            services.TryAddSingleton<IBus>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Bus>>();
                var busFailureHandlingService = serviceProvider.GetRequiredService<IBusFailureHandlingService>();

                var namingConventions = serviceProvider.GetRequiredService<INamingConventions>();

                return new Bus(
                    brokerOptions,
                    busFailureHandlingService,
                    logger,
                    namingConventions,
                    busResilienceOptions);
            });

            return services;
        }
    }
}