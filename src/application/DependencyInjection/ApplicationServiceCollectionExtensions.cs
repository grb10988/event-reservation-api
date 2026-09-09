using System.Reflection;
using EventReservation.Application.Abstractions;
using EventReservation.Application.Abstractions.DomainEvents;
using EventReservation.Application.Abstractions.Requests;
using EventReservation.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services
            .AddSingleton(TimeProvider.System)
            .AddScoped<IDispatcher, Dispatcher>()
            .AddRequestPipeline()
            .AddDomainEventPipeline()
            .AddRequestHandlers(assembly)
            .AddDomainEventHandlers(assembly);

        return services;
    }

    private static IServiceCollection AddRequestPipeline(this IServiceCollection services) =>
        services
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestTracingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(RequestMetricsBehavior<,>));

    private static IServiceCollection AddDomainEventPipeline(this IServiceCollection services) =>
        services
            .AddScoped(typeof(IDomainEventPipelineBehavior<>), typeof(DomainEventTracingBehavior<>))
            .AddScoped(typeof(IDomainEventPipelineBehavior<>), typeof(DomainEventLoggingBehavior<>))
            .AddScoped(typeof(IDomainEventPipelineBehavior<>), typeof(DomainEventMetricsBehavior<>));

    private static IServiceCollection AddRequestHandlers(this IServiceCollection services, Assembly assembly)
    {
        RegisterHandlers(services, assembly, typeof(ICommandHandler<,>));
        RegisterHandlers(services, assembly, typeof(IQueryHandler<,>));
        return services;
    }

    private static IServiceCollection AddDomainEventHandlers(this IServiceCollection services, Assembly assembly)
    {
        RegisterHandlers(services, assembly, typeof(IDomainEventHandler<>));
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly, Type openGenericInterface)
    {
        var registrations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(implementation => implementation.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGenericInterface)
                .Select(serviceType => (serviceType, implementation)));

        foreach (var (serviceType, implementation) in registrations)
            services.AddScoped(serviceType, implementation);
    }
}