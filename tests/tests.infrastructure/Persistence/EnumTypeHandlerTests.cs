using System.Data;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence;
using NSubstitute;

namespace EventReservation.Tests.Infrastructure.Persistence;

[TestClass]
public class EnumTypeHandlerTests
{
    private EnumTypeHandler<SeatStatus> _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _handler = new EnumTypeHandler<SeatStatus>();
    }

    // ============================================================
    // SetValue
    // ============================================================

    [TestMethod]
    public void SetValue_SetsParameterDbTypeToString_AndValueToEnumName()
    {
        // Arrange
        var parameter = Substitute.For<IDbDataParameter>();

        // Act
        _handler.SetValue(parameter, SeatStatus.Held);

        // Assert
        Assert.AreEqual(DbType.String, parameter.DbType);
        Assert.AreEqual("Held", parameter.Value);
    }

    // ============================================================
    // Parse
    // ============================================================

    [TestMethod]
    public void Parse_WithValidEnumName_ReturnsCorrectEnumValue()
    {
        // Act
        var result = _handler.Parse("Reserved");

        // Assert
        Assert.AreEqual(SeatStatus.Reserved, result);
    }

    [TestMethod]
    [DataRow("NotARealStatus")]
    [DataRow(12345)]
    [DataRow(null)]
    public void Parse_WithInvalidOrNullName_ReturnsDefaultEnumValue(object input)
    {
        // Act
        var result = _handler.Parse(input);

        // Assert
        Assert.AreEqual(default, result);
    }

    [TestMethod]
    public void SetValueThenParse_RoundTripsToTheSameEnumValue()
    {
        // Arrange
        var parameter = Substitute.For<IDbDataParameter>();

        // Act
        _handler.SetValue(parameter, SeatStatus.Available);
        var result = _handler.Parse(parameter.Value!);

        // Assert
        Assert.AreEqual(SeatStatus.Available, result);
    }
}