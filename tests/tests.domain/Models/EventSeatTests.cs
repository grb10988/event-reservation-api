using EventReservation.Domain.Models;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class EventSeatTests
{
    private static readonly Guid ValidEventId = Guid.NewGuid();
    private static readonly Guid ValidSeatId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();

    // ============================================================
    // Create
    // ============================================================

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithAvailableStatus()
    {
        var result = EventSeat.Create(ValidEventId, ValidSeatId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidEventId, result.Value.EventId);
        Assert.AreEqual(ValidSeatId, result.Value.SeatId);
        Assert.AreEqual(EventSeatStatus.Available, result.Value.Status);
        Assert.IsNull(result.Value.HeldBy);
    }

    [TestMethod]
    public void Create_WithEmptyEventId_ReturnsFailureWithEmptyEventIdError()
    {
        var result = EventSeat.Create(Guid.Empty, ValidSeatId);

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.EmptyEventId);
    }

    [TestMethod]
    public void Create_WithEmptySeatId_ReturnsFailureWithEmptySeatIdError()
    {
        var result = EventSeat.Create(ValidEventId, Guid.Empty);

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.EmptySeatId);
    }

    // ============================================================
    // Hold
    // ============================================================

    [TestMethod]
    public void Hold_WhenAvailable_ReturnsSuccessWithHeldStatus_AndRecordsTheFencingToken()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);

        var result = eventSeat.Hold(ReservationId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventSeatStatus.Held, result.Value.Status);
        Assert.AreEqual(ReservationId, result.Value.HeldBy);
    }

    [TestMethod]
    public void Hold_WhenAlreadyHeld_ReturnsFailureWithCannotHoldError()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);

        var result = eventSeat.Hold(Guid.NewGuid());

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.CannotHold);
    }

    [TestMethod]
    public void Hold_WhenReserved_ReturnsFailureWithCannotHoldError()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);
        eventSeat.Reserve(ReservationId);

        var result = eventSeat.Hold(ReservationId);

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.CannotHold);
    }

    // ============================================================
    // Reserve
    // ============================================================

    [TestMethod]
    public void Reserve_WhenHeldByThisReservation_ReturnsSuccessWithReservedStatus()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);

        var result = eventSeat.Reserve(ReservationId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventSeatStatus.Reserved, result.Value.Status);
    }

    [TestMethod]
    public void Reserve_WhenHeldByADifferentReservation_ReturnsFailureWithCannotReserveError()
    {
        // The fencing check, not just the status check - this is the case a plain
        // status-only state machine would have missed
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);

        var result = eventSeat.Reserve(Guid.NewGuid());

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.CannotReserve);
    }

    [TestMethod]
    public void Reserve_WhenAvailable_ReturnsFailureWithCannotReserveError()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);

        var result = eventSeat.Reserve(ReservationId);

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.CannotReserve);
    }

    // ============================================================
    // Release
    // ============================================================

    [TestMethod]
    public void Release_WhenHeldByThisReservation_ReturnsSuccessWithAvailableStatus_AndClearsTheFencingToken()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);

        var result = eventSeat.Release(ReservationId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventSeatStatus.Available, result.Value.Status);
        Assert.IsNull(result.Value.HeldBy);
    }

    [TestMethod]
    public void Release_WhenReservedByThisReservation_ReturnsSuccessWithAvailableStatus()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);
        eventSeat.Reserve(ReservationId);

        var result = eventSeat.Release(ReservationId);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventSeatStatus.Available, result.Value.Status);
    }

    [TestMethod]
    public void Release_WhenHeldByADifferentReservation_ReturnsFailureWithCannotReleaseError()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);
        eventSeat.Hold(ReservationId);

        var result = eventSeat.Release(Guid.NewGuid());

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.CannotRelease);
    }

    [TestMethod]
    public void Release_WhenAvailable_ReturnsFailureWithCannotReleaseError()
    {
        var eventSeat = EventSeat.Create(ValidEventId, ValidSeatId).Value;
        Assert.IsNotNull(eventSeat);

        var result = eventSeat.Release(ReservationId);

        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), EventSeat.Errors.CannotRelease);
    }

    // ============================================================
    // Rehydrate
    // ============================================================

    [TestMethod]
    public void Rehydrate_WithGivenValues_ReturnsEventSeatWithThoseExactValues()
    {
        var eventSeat = EventSeat.Rehydrate(ValidEventId, ValidSeatId, EventSeatStatus.Held, ReservationId);

        Assert.AreEqual(ValidEventId, eventSeat.EventId);
        Assert.AreEqual(ValidSeatId, eventSeat.SeatId);
        Assert.AreEqual(EventSeatStatus.Held, eventSeat.Status);
        Assert.AreEqual(ReservationId, eventSeat.HeldBy);
    }
}