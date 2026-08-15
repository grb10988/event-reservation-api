using EventReservation.Infrastructure.Persistence;
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
            .AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();

        return services;
    }
}