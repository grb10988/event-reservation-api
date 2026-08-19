using System.Reflection;
using EventReservation.Application.Abstractions;
using EventReservation.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services
            .AddScoped<IDispatcher, Dispatcher>()
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));

        RegisterHandlers(services, assembly, typeof(ICommandHandler<,>));
        RegisterHandlers(services, assembly, typeof(IQueryHandler<,>));

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