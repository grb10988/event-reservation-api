using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EventReservation.Infrastructure.Persistence;

public sealed class NpgsqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' was not found.");

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}