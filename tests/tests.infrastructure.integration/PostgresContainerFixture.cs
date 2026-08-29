using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace EventReservation.Tests.Infrastructure.Integration;

[TestClass]
public static class PostgresContainerFixture
{
    private static PostgreSqlContainer? _container;
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static bool _isInitialized = false;

    public static string ConnectionString { get; private set; } = null!;

    public static async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;

        await _semaphore.WaitAsync();

        try
        {
            _container = new PostgreSqlBuilder("postgres:16")
                .WithDatabase("eventreservation")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _container.StartAsync();

            ConnectionString = _container.GetConnectionString();

            var schemaSql = await File.ReadAllTextAsync(FindSchemaFilePath());

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var batch = new NpgsqlBatch(connection);

            await using var command = connection.CreateCommand();
            command.CommandText = schemaSql;
            await command.ExecuteNonQueryAsync();

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private static string FindSchemaFilePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "database", "schema.sql")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate database/schema.sql relative to the test output directory.");

        return Path.Combine(directory.FullName, "database", "schema.sql");
    }
}