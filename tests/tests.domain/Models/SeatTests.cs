using EventReservation.Domain.Models;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class SeatTests
{
    private static readonly Guid ValidEventId = Guid.CreateVersion7();
    private const string ValidSection = "A";
    private const int ValidRow = 3;
    private const int ValidNumber = 12;

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithExpectedValues()
    {
        // Act
        var result = Seat.Create(ValidEventId, ValidSection, ValidRow, ValidNumber);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidEventId, result.Value.EventId);
        Assert.AreEqual(ValidSection, result.Value.Section);
        Assert.AreEqual(ValidRow, result.Value.Row);
        Assert.AreEqual(ValidNumber, result.Value.Number);
        Assert.AreEqual(SeatStatus.Available, result.Value.Status);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    public void Create_WithEmptyEventId_ReturnsFailureWithEmptyEventIdError()
    {
        // Act
        var result = Seat.Create(Guid.Empty, ValidSection, ValidRow, ValidNumber);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.EmptyEventId);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidSection_ReturnsFailureWithEmptySectionError(string? section)
    {
        // Act
        var result = Seat.Create(ValidEventId, section!, ValidRow, ValidNumber);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.EmptySection);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_WithInvalidRow_ReturnsFailureWithEmptyRowError(int row)
    {
        // Act
        var result = Seat.Create(ValidEventId, ValidSection, row, ValidNumber);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.EmptyRow);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_WithInvalidNumber_ReturnsFailureWithEmptyNumberError(int number)
    {
        // Act
        var result = Seat.Create(ValidEventId, ValidSection, ValidRow, number);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.EmptyNumber);
    }

    [TestMethod]
    public void Create_WithAllInvalidInputs_AccumulatesAllFourErrors()
    {
        // Act
        var result = Seat.Create(Guid.Empty, "", 0, 0);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(4, result.Errors.Count);
    }

    [TestMethod]
    public void Create_CalledTwiceWithSameInputs_GeneratesDifferentIds()
    {
        // Act
        var first = Seat.Create(ValidEventId, ValidSection, ValidRow, ValidNumber);
        var second = Seat.Create(ValidEventId, ValidSection, ValidRow, ValidNumber);

        // Assert
        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreNotEqual(first.Value.Id, second.Value.Id);
    }
}