using System.Data;
using EventReservation.Infrastructure.Persistence;
using Npgsql;

namespace EventReservation.Tests.Infrastructure.Integration;

[TestClass]
public abstract class IntegrationTestBase
{
    protected IDbConnectionFactory ConnectionFactory { get; private set; } = null!;

    [TestInitialize]
    public void BaseSetup()
    {
        ConnectionFactory = new TestConnectionFactory(PostgresContainerFixture.ConnectionString);
    }

    [TestCleanup]
    public async Task BaseCleanupAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresContainerFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = "TRUNCATE TABLE order_reservations, orders, reservations, seats, events, customers, venues RESTART IDENTITY CASCADE;";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new NpgsqlConnection(connectionString);
    }
}