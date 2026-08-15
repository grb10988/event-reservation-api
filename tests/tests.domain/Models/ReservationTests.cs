using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class ReservationTests
{
    private static readonly Guid ValidSeatId = Guid.NewGuid();
    private static readonly Guid ValidEventId = Guid.NewGuid();
    private static readonly Guid ValidCustomerId = Guid.NewGuid();
    private const decimal ValidPrice = 50.00m;
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _timeProvider = new FakeTimeProvider(Now);
    }

    // ============================================================
    // Create
    // ============================================================

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithExpectedValues()
    {
        // Act
        var result = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidSeatId, result.Value.SeatId);
        Assert.AreEqual(ValidEventId, result.Value.EventId);
        Assert.AreEqual(ValidCustomerId, result.Value.CustomerId);
        Assert.AreEqual(ValidPrice, result.Value.Price);
        Assert.AreEqual(ReservationStatus.Held, result.Value.Status);
        Assert.AreEqual(Now, result.Value.CreatedAt);
        Assert.AreEqual(Now + Reservation.DefaultHoldDuration, result.Value.HoldExpiresAt);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    public void Create_WithEmptySeatId_ReturnsFailureWithEmptySeatIdError()
    {
        // Act
        var result = Reservation.Create(Guid.Empty, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.EmptySeatId);
    }

    [TestMethod]
    public void Create_WithEmptyEventId_ReturnsFailureWithEmptyEventIdError()
    {
        // Act
        var result = Reservation.Create(ValidSeatId, Guid.Empty, ValidCustomerId, ValidPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.EmptyEventId);
    }

    [TestMethod]
    public void Create_WithEmptyCustomerId_ReturnsFailureWithEmptyCustomerIdError()
    {
        // Act
        var result = Reservation.Create(ValidSeatId, ValidEventId, Guid.Empty, ValidPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.EmptyCustomerId);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-100)]
    public void Create_WithNegativePrice_ReturnsFailureWithInvalidPriceError(int price)
    {
        // Act
        var result = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, price, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.InvalidPrice);
    }

    [TestMethod]
    public void Create_WithZeroPrice_ReturnsSuccess()
    {
        // Act
        var result = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, 0m, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0m, result.Value.Price);
    }

    [TestMethod]
    public void Create_WithCustomHoldDuration_ReturnsSuccessWithExpectedHoldExpiresAt()
    {
        // Arrange
        var customDuration = TimeSpan.FromMinutes(30);

        // Act
        var result = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider, customDuration);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Now + customDuration, result.Value.HoldExpiresAt);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    public void Create_WithNonPositiveHoldDuration_ReturnsFailureWithInvalidHoldDurationError(int minutes)
    {
        // Arrange
        var invalidDuration = TimeSpan.FromMinutes(minutes);

        // Act
        var result = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider, invalidDuration);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.InvalidHoldDuration);
    }

    [TestMethod]
    public void Create_WithAllInvalidInputs_AccumulatesAllErrors()
    {
        // Act
        var result = Reservation.Create(Guid.Empty, Guid.Empty, Guid.Empty, -1m, _timeProvider, TimeSpan.Zero);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(5, result.Errors.Count);
    }

    // ============================================================
    // Confirm
    // ============================================================

    [TestMethod]
    public void Confirm_WhenHeld_ReturnsSuccessWithConfirmedStatus()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);

        // Act
        var result = reservation.Confirm();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ReservationStatus.Confirmed, result.Value.Status);
    }

    [TestMethod]
    public void Confirm_WhenAlreadyConfirmed_ReturnsFailureWithCannotConfirmError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Confirm();

        // Act
        var result = reservation.Confirm();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotConfirm);
    }

    [TestMethod]
    public void Confirm_WhenCancelled_ReturnsFailureWithCannotConfirmError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Cancel();

        // Act
        var result = reservation.Confirm();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotConfirm);
    }

    [TestMethod]
    public void Confirm_WhenExpired_ReturnsFailureWithCannotConfirmError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Expire();

        // Act
        var result = reservation.Confirm();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotConfirm);
    }

    // ============================================================
    // Cancel
    // ============================================================

    [TestMethod]
    public void Cancel_WhenHeld_ReturnsSuccessWithCancelledStatus()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);

        // Act
        var result = reservation.Cancel();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ReservationStatus.Cancelled, result.Value.Status);
    }

    [TestMethod]
    public void Cancel_WhenConfirmed_ReturnsSuccessWithCancelledStatus()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Confirm();

        // Act
        var result = reservation.Cancel();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ReservationStatus.Cancelled, result.Value.Status);
    }

    [TestMethod]
    public void Cancel_WhenAlreadyCancelled_ReturnsFailureWithCannotCancelError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Cancel();

        // Act
        var result = reservation.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotCancel);
    }

    [TestMethod]
    public void Cancel_WhenExpired_ReturnsFailureWithCannotCancelError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Expire();

        // Act
        var result = reservation.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotCancel);
    }

    // ============================================================
    // Expire
    // ============================================================

    [TestMethod]
    public void Expire_WhenHeld_ReturnsSuccessWithExpiredStatus()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);

        // Act
        var result = reservation.Expire();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ReservationStatus.Expired, result.Value.Status);
    }

    [TestMethod]
    public void Expire_WhenConfirmed_ReturnsFailureWithCannotExpireError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Confirm();

        // Act
        var result = reservation.Expire();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotExpire);
    }

    [TestMethod]
    public void Expire_WhenCancelled_ReturnsFailureWithCannotExpireError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Cancel();

        // Act
        var result = reservation.Expire();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotExpire);
    }

    [TestMethod]
    public void Expire_WhenAlreadyExpired_ReturnsFailureWithCannotExpireError()
    {
        // Arrange
        var reservation = Reservation.Create(ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        reservation.Expire();

        // Act
        var result = reservation.Expire();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotExpire);
    }

    // ============================================================
    // Rehydrate
    // ============================================================

    [TestMethod]
    public void Rehydrate_WithGivenValues_ReturnsReservationWithThoseExactValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = Now.AddDays(-1);
        var holdExpiresAt = createdAt.AddMinutes(15);

        // Act
        var reservation = Reservation.Rehydrate(id, ValidSeatId, ValidEventId, ValidCustomerId, ValidPrice, ReservationStatus.Confirmed, createdAt, holdExpiresAt);

        // Assert
        Assert.AreEqual(id, reservation.Id);
        Assert.AreEqual(ValidSeatId, reservation.SeatId);
        Assert.AreEqual(ValidEventId, reservation.EventId);
        Assert.AreEqual(ValidCustomerId, reservation.CustomerId);
        Assert.AreEqual(ValidPrice, reservation.Price);
        Assert.AreEqual(ReservationStatus.Confirmed, reservation.Status);
        Assert.AreEqual(createdAt, reservation.CreatedAt);
        Assert.AreEqual(holdExpiresAt, reservation.HoldExpiresAt);
    }
}