using EventReservation.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace EventReservation.Tests.Infrastructure.Persistence;

[TestClass]
public class DatabaseExceptionMapperTests
{
    private static readonly NullLogger<DatabaseExceptionMapperTests> Logger =
        NullLogger<DatabaseExceptionMapperTests>.Instance;

    // ============================================================
    // Map
    // ============================================================

    [TestMethod]
    public void Map_WithNpgsqlExceptionThatIsNotAPostgresException_ReturnsDatabaseUnavailable()
    {
        // Arrange - simulates the connection never reaching the server at
        // all (connection refused, DNS failure) - no SqlState exists for
        // this case, since Postgres itself never got involved
        var exception = new NpgsqlException("Connection refused");

        // Act
        var error = DatabaseExceptionMapper.Map(exception, Logger);

        // Assert
        Assert.AreEqual(DatabaseExceptionMapper.Errors.DatabaseUnavailable, error);
    }

    [TestMethod]
    public void Map_WithSomeUnrelatedException_ReturnsGenericDatabaseError()
    {
        // Arrange
        var exception = new InvalidCastException("unrelated");

        // Act
        var error = DatabaseExceptionMapper.Map(exception, Logger);

        // Assert
        Assert.AreEqual(DatabaseExceptionMapper.Errors.GenericDatabaseError, error);
    }
}