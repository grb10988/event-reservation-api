using EventReservation.Domain.Results;
using EventReservation.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;

namespace EventReservation.Tests.Infrastructure.Persistence;

[TestClass]
public class DatabaseExceptionMapperTests
{
    private ILogger<DatabaseExceptionMapperTests> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<DatabaseExceptionMapperTests>>();
    }

    private void AssertLoggedError(Exception exception) =>
        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            exception,
            Arg.Any<Func<object, Exception?, string>>());

    // ============================================================
    // Map
    // ============================================================

    [TestMethod]
    public void Map_WithNpgsqlExceptionThatIsNotAPostgresException_ReturnsDatabaseUnavailable_AndLogsError()
    {
        // Arrange - simulates the connection never reaching the server at
        // all (connection refused, DNS failure) - no SqlState exists for
        // this case, since Postgres itself never got involved
        var exception = new NpgsqlException("Connection refused");

        // Act
        var error = DatabaseExceptionMapper.Map(exception, _logger);

        // Assert
        Assert.AreEqual(DatabaseExceptionMapper.Errors.DatabaseUnavailable, error);
        AssertLoggedError(exception);
    }

    [TestMethod]
    public void Map_WithSomeUnrelatedException_ReturnsGenericDatabaseError_AndLogsError()
    {
        // Arrange
        var exception = new InvalidCastException("unrelated");

        // Act
        var error = DatabaseExceptionMapper.Map(exception, _logger);

        // Assert
        Assert.AreEqual(DatabaseExceptionMapper.Errors.GenericDatabaseError, error);
        AssertLoggedError(exception);
    }

    // ============================================================
    // ShouldLog - the actual "does not log" coverage
    //
    // Tested directly against ErrorCategory rather than through Map()
    // with a real PostgresException, since PostgresException has no
    // public constructor - there's no way to fabricate a real
    // UniqueViolation/ForeignKeyViolation exception outside the Npgsql
    // assembly. Testing the pure category -> should-log decision gives
    // complete coverage of the rule without that limitation - the
    // Conflict/Validation "stays silent" cases that Map() itself can't
    // exercise here are covered instead.
    // ============================================================

    [TestMethod]
    [DataRow(ErrorCategory.Validation, false)]
    [DataRow(ErrorCategory.NotFound, false)]
    [DataRow(ErrorCategory.Conflict, false)]
    [DataRow(ErrorCategory.Unavailable, true)]
    [DataRow(ErrorCategory.Unexpected, true)]
    public void ShouldLog_ForEachCategory_ReturnsExpectedResult(ErrorCategory category, bool expected)
    {
        // Act
        var result = DatabaseExceptionMapper.ShouldLog(category);

        // Assert
        Assert.AreEqual(expected, result);
    }
}