using EventReservation.Domain.Models;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class SeatTests
{
    private static readonly Guid ValidVenueId = Guid.NewGuid();
    private const string ValidSection = "A";
    private const int ValidRow = 3;
    private const int ValidNumber = 12;

    // ============================================================
    // Create
    // ============================================================

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithExpectedValues()
    {
        // Act
        var result = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidSection, result.Value.Section);
        Assert.AreEqual(ValidRow, result.Value.Row);
        Assert.AreEqual(ValidNumber, result.Value.Number);
        Assert.AreEqual(SeatStatus.Available, result.Value.Status);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidSection_ReturnsFailureWithEmptySectionError(string? section)
    {
        // Act
        var result = Seat.Create(ValidVenueId, section!, ValidRow, ValidNumber);

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
        var result = Seat.Create(ValidVenueId, ValidSection, row, ValidNumber);

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
        var result = Seat.Create(ValidVenueId, ValidSection, ValidRow, number);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.EmptyNumber);
    }

    [TestMethod]
    public void Create_WithAllInvalidInputs_AccumulatesAllErrors()
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
        var first = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber);
        var second = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber);

        // Assert
        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreNotEqual(first.Value.Id, second.Value.Id);
    }

    // ============================================================
    // Hold
    // ============================================================

    [TestMethod]
    public void Hold_WhenAvailable_ReturnsSuccessWithHeldStatus()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);

        // Act
        var result = seat.Hold();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SeatStatus.Held, result.Value.Status);
    }

    [TestMethod]
    public void Hold_WhenAlreadyHeld_ReturnsFailureWithCannotHoldError()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        // Act
        var result = seat.Hold();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotHold);
    }

    [TestMethod]
    public void Hold_WhenReserved_ReturnsFailureWithCannotHoldError()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);
        seat.Hold();
        seat.Reserve();

        // Act
        var result = seat.Hold();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotHold);
    }

    // ============================================================
    // Reserve
    // ============================================================

    [TestMethod]
    public void Reserve_WhenHeld_ReturnsSuccessWithReservedStatus()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        // Act
        var result = seat.Reserve();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SeatStatus.Reserved, result.Value.Status);
    }

    [TestMethod]
    public void Reserve_WhenAvailable_ReturnsFailureWithCannotReserveError()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);

        // Act
        var result = seat.Reserve();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotReserve);
    }

    [TestMethod]
    public void Reserve_WhenAlreadyReserved_ReturnsFailureWithCannotReserveError()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);
        seat.Hold();
        seat.Reserve();

        // Act
        var result = seat.Reserve();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotReserve);
    }

    // ============================================================
    // Release
    // ============================================================

    [TestMethod]
    public void Release_WhenHeld_ReturnsSuccessWithAvailableStatus()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        // Act
        var result = seat.Release();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SeatStatus.Available, result.Value.Status);
    }

    [TestMethod]
    public void Release_WhenReserved_ReturnsSuccessWithAvailableStatus()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);
        seat.Hold();
        seat.Reserve();

        // Act
        var result = seat.Release();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SeatStatus.Available, result.Value.Status);
    }

    [TestMethod]
    public void Release_WhenAvailable_ReturnsFailureWithCannotReleaseError()
    {
        // Arrange
        var seat = Seat.Create(ValidVenueId, ValidSection, ValidRow, ValidNumber).Value;
        Assert.IsNotNull(seat);

        // Act
        var result = seat.Release();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotRelease);
    }
}