using EventReservation.Application.Abstractions;
using EventReservation.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services
            .AddScoped<IDispatcher, Dispatcher>()
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(TracingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>))
            .AddScoped(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));

        return services;
    }
}