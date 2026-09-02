using DotNet.Testcontainers.Builders;
using EventReservation.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventReservation.Tests.Infrastructure.Integration;

[TestClass]
public static class PostgresContainerFixture
{
    private static PostgreSqlContainer? _container;
    public static string ConnectionString { get; private set; } = null!;

    [AssemblyInitialize]
    public static async Task AssemblyInitialize(TestContext context)
    {
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        DapperTypeHandlerRegistration.RegisterTypeHandlers();
        
        try
        {
            _container = new PostgreSqlBuilder("postgres:16")
                    .WithDatabase("eventreservation")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();

            await _container.StartAsync();
        }
        catch (DockerUnavailableException ex)
        {
            throw new InvalidOperationException(
                "Docker Desktop must be running to execute the Infrastructure integration tests. Start Docker Desktop and re-run the tests.", ex);
        }

        ConnectionString = _container.GetConnectionString();

        var schemaSql = await File.ReadAllTextAsync(FindSchemaFilePath());

        schemaSql = schemaSql
            .Replace(":'api_password'", "'test_api_password'")
            .Replace(":'admin_password'", "'test_admin_password'");

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = schemaSql;
        await command.ExecuteNonQueryAsync();
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        if (_container is not null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _container.StopAsync(cts.Token);
            await _container.DisposeAsync();
        }
    }

    private static string FindSchemaFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "database", "scripts", "schema.sql")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate database/scripts/schema.sql relative to the test output directory.");

        return Path.Combine(directory.FullName, "database", "scripts", "schema.sql");
    }
}