using System.Reflection;
using EventReservation.Application.Abstractions.IntegrationEvents;
using EventReservation.Application.Behaviors;
using EventReservation.Infrastructure.Messaging;
using EventReservation.Infrastructure.Messaging.Consumers;
using EventReservation.Infrastructure.Messaging.Publishers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EventReservation.Infrastructure.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(
            this IServiceCollection services,
            IConfiguration configuration,
            Assembly messagingAssembly)
    {
        services
            .Configure<RabbitMqPublisherSettings>(configuration.GetSection("RabbitMQ"))
            .AddHostedService<RabbitMqTopologyInitializer>()
            .AddSingleton<IConnectionFactory>(sp =>
            {
                var hostName = configuration["RabbitMQ:HostName"]
                    ?? throw new InvalidOperationException("RabbitMQ:HostName configuration is missing.");

                var username = configuration["RabbitMQ:Username"]
                    ?? throw new InvalidOperationException("RabbitMQ:Username configuration is missing.");

                var password = configuration["RabbitMQ:Password"]
                    ?? throw new InvalidOperationException("RabbitMQ:Password configuration is missing.");

                return new ConnectionFactory
                {
                    HostName = hostName,
                    UserName = username,
                    Password = password
                };

            })
            .AddSingleton(sp =>
            {
                var factory = sp.GetRequiredService<IConnectionFactory>();
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            })
            .AddIntegrationEventPublisher();

        var eventTypes = RegisterIntegrationEventHandlersAndCollectEventTypes(services, messagingAssembly);
        RegisterConsumerWorkers(services, configuration, messagingAssembly);

        return services;
    }

    private static IReadOnlyCollection<Type> RegisterIntegrationEventHandlersAndCollectEventTypes(
        IServiceCollection services,
        Assembly assembly)
    {
        var openGenericInterface = typeof(IIntegrationEventHandler<>);

        var matches = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(implementation => implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface)
                .Select(serviceType => (serviceType, implementation)))
            .ToList();

        foreach (var (serviceType, implementation) in matches)
            services.AddScoped(serviceType, implementation);

        return matches
            .Select(m => m.serviceType.GetGenericArguments()[0])
            .Distinct()
            .ToList();
    }

    private static void RegisterConsumerWorkers(
        IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly)
    {
        var openGenericInterface = typeof(IIntegrationEventHandler<>);

        var eventTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface)
            .Select(i => i.GetGenericArguments()[0])
            .Distinct();

        foreach (var eventType in eventTypes)
        {
            var settingsSection = configuration.GetSection($"RabbitMQ:Consumers:{eventType.Name}");

            var optionsType = typeof(Options);

            services.Configure<RabbitMqConsumerSettings>(settingsSection);

            var workerType = typeof(RabbitMqConsumerWorker<>).MakeGenericType(eventType);

            services.AddSingleton(typeof(IHostedService), workerType);
        }
    }

    private static IServiceCollection AddIntegrationEventPublisher(this IServiceCollection services)
    {
        services
            .AddScoped<RabbitMqPublisher>()
            .AddScoped(sp =>
            {
                IEventPublisher publisher = sp.GetRequiredService<RabbitMqPublisher>();
                publisher = new IntegrationEventMetricsBehavior(publisher);
                publisher = new IntegrationEventLoggingBehavior(publisher, sp.GetRequiredService<ILogger<IntegrationEventLoggingBehavior>>());
                publisher = new IntegrationEventTracingBehavior(publisher);
                return publisher;
            });

        return services;
    }
}