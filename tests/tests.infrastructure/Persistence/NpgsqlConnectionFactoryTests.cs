using EventReservation.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace EventReservation.Tests.Infrastructure.Persistence;

[TestClass]
public class NpgsqlConnectionFactoryTests
{
    // ============================================================
    // Constructor
    // ============================================================

    [TestMethod]
    public void Constructor_WithMissingConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange - no "Default" connection string configured at all
        var configuration = Substitute.For<IConfiguration>();
        configuration.GetConnectionString("Default").Returns((string?)null);

        // Assert
        Assert.ThrowsException<InvalidOperationException>(() => new NpgsqlConnectionFactory(configuration));
    }

    [TestMethod]
    public void Constructor_WithConnectionStringPresent_DoesNotThrow()
    {
        // Arrange
        var configuration = Substitute.For<IConfiguration>();
        configuration.GetConnectionString("Default").Returns("Host=localhost;Database=test;Username=test;Password=test");

        // Act
        var factory = new NpgsqlConnectionFactory(configuration);

        // Assert
        Assert.IsNotNull(factory);
    }

    [TestMethod]
    public void CreateConnection_ReturnsAClosedConnection()
    {
        // Arrange - proves the deliberate "don't eagerly open" design from
        // when this factory was first built; Dapper is responsible for
        // opening, not this factory
        var configuration = Substitute.For<IConfiguration>();
        configuration.GetConnectionString("Default").Returns("Host=localhost;Database=test;Username=test;Password=test");
        var factory = new NpgsqlConnectionFactory(configuration);

        // Act
        using var connection = factory.CreateConnection();

        // Assert
        Assert.AreEqual(System.Data.ConnectionState.Closed, connection.State);
    }
}