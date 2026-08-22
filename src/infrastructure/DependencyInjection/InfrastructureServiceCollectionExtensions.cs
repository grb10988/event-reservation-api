using EventReservation.Application.Interfaces;
using EventReservation.Infrastructure.Persistence;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        DapperTypeHandlerRegistration.RegisterTypeHandlers();

        services
            .AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>()

            .AddScoped<IVenueRepository, VenueRepository>()
            .AddScoped<ISeatRepository, SeatRepository>()
            .AddScoped<IEventRepository, EventRepository>()
            .AddScoped<IReservationRepository, ReservationRepository>()
            .AddScoped<IOrderRepository, OrderRepository>()
            .AddScoped<ICustomerRepository, CustomerRepository>();

        return services;
    }
}