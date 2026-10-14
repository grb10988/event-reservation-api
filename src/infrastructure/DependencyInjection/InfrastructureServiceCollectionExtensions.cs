using EventReservation.Application.Interfaces;
using EventReservation.Infrastructure.BackgroundServices;
using EventReservation.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        DapperTypeHandlerRegistration.RegisterTypeHandlers();

        services
            .AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>()
            .AddRepositories()
            .Configure<ReservationHoldSweepSettings>(configuration.GetSection("ReservationHoldSweep"))
            .AddHostedService<ReservationHoldSweepWorker>();

        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        var assembly = typeof(InfrastructureServiceCollectionExtensions).Assembly;

        var registrations = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(implementation => implementation.GetInterfaces()
                .Where(i => i != typeof(IRepository) && typeof(IRepository).IsAssignableFrom(i))
                .Select(serviceType => (serviceType, implementation)));

        foreach (var (serviceType, implementation) in registrations)
            services.AddScoped(serviceType, implementation);

        return services;
    }
}