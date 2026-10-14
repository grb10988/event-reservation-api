using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class EventSeatRepositoryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = TruncateToSeconds(DateTimeOffset.UtcNow);

    private IVenueRepository _venueRepository = null!;
    private ISeatRepository _seatRepository = null!;
    private IEventRepository _eventRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private FakeTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = new VenueRepository(ConnectionFactory, NullLogger<VenueRepository>.Instance);
        _seatRepository = new SeatRepository(ConnectionFactory, NullLogger<SeatRepository>.Instance);
        _eventRepository = new EventRepository(ConnectionFactory, NullLogger<EventRepository>.Instance);
        _eventSeatRepository = new EventSeatRepository(ConnectionFactory, NullLogger<EventSeatRepository>.Instance);
        _timeProvider = new FakeTimeProvider(Now);
    }

    private async Task<Guid> SeedVenueAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedSeatAsync(Guid venueId, string section = "A", int row = 1, int number = 1)
    {
        var seat = Seat.Create(venueId, section, row, number).Value;
        Assert.IsNotNull(seat);
        var result = await _seatRepository.AddAsync(seat);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedEventAsync(Guid venueId)
    {
        var evt = Event.Create(venueId, "Summer Concert", "An outdoor concert.", Now.AddDays(30), Now.AddDays(30).AddHours(3), 50.00m, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var result = await _eventRepository.AddAsync(evt);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    // Seeds one venue, one seat, one event, and provisions event_seats for
    // that pair - the Available state every CAS test starts from.
    private async Task<(Guid SeatId, Guid EventId, Guid VenueId)> SeedAvailableEventSeatAsync(
    string section = "A", int row = 1, int number = 1)
    {
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId, section, row, number);
        var eventId = await SeedEventAsync(venueId);
        var publishResult = await _eventRepository.TryPublishAsync(eventId);
        Assert.IsTrue(publishResult is { IsSuccess: true, Value: true }, string.Join("; ", publishResult.ToFailureMessages()));
        var provisionResult = await _eventSeatRepository.ProvisionAsync(eventId, venueId);
        Assert.IsTrue(provisionResult.IsSuccess, string.Join("; ", provisionResult.ToFailureMessages()));
        return (seatId, eventId, venueId);
    }

    // ============================================================
    // ProvisionAsync / GetByIdAsync
    // ============================================================

    [TestMethod]
    public async Task ProvisionAsync_ThenGetByIdAsync_CreatesOneRowPerVenueSeat_AllAvailable()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var eventId = await SeedEventAsync(venueId);

        // Act
        var provisionResult = await _eventSeatRepository.ProvisionAsync(eventId, venueId);
        var getResult1 = await _eventSeatRepository.GetByIdAsync(seatId1, eventId);
        var getResult2 = await _eventSeatRepository.GetByIdAsync(seatId2, eventId);

        // Assert
        Assert.IsTrue(provisionResult.IsSuccess);
        Assert.IsNotNull(getResult1.Value);
        Assert.AreEqual(EventSeatStatus.Available, getResult1.Value.Status);
        Assert.IsNull(getResult1.Value.HeldBy);
        Assert.IsNotNull(getResult2.Value);
        Assert.AreEqual(EventSeatStatus.Available, getResult2.Value.Status);
    }

    [TestMethod]
    public async Task ProvisionAsync_CalledTwice_IsIdempotent_AndDoesNotDisturbExistingHolds()
    {
        // Arrange - proves ON CONFLICT DO NOTHING actually protects a seat
        // that's already held from being silently reset if Publish retries
        var (seatId, eventId, venueId) = await SeedAvailableEventSeatAsync();
        var reservationId = Guid.NewGuid();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, reservationId);

        // Act
        var secondProvision = await _eventSeatRepository.ProvisionAsync(eventId, venueId);
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(secondProvision.IsSuccess);
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Held, getResult.Value.Status);
        Assert.AreEqual(reservationId, getResult.Value.HeldBy);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenNoRowExistsForThatSeatAndEvent_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _eventSeatRepository.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // GetByEventIdAsync
    // ============================================================

    [TestMethod]
    public async Task GetByEventIdAsync_ReturnsOnlySeatsForThatEvent()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        await SeedSeatAsync(venueId, "B", 1, 1);
        await SeedSeatAsync(venueId, "A", 2, 1);
        await SeedSeatAsync(venueId, "A", 1, 1);
        var eventId = await SeedEventAsync(venueId);
        var otherEventId = await SeedEventAsync(venueId);
        await _eventSeatRepository.ProvisionAsync(eventId, venueId);
        await _eventSeatRepository.ProvisionAsync(otherEventId, venueId);

        // Act
        var result = await _eventSeatRepository.GetByEventIdAsync(eventId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(3, result.Value);
        Assert.IsTrue(result.Value.All(es => es.EventId == eventId));
    }

    // ============================================================
    // TryHoldAsync
    // ============================================================

    [TestMethod]
    public async Task TryHoldAsync_WhenAvailable_ReturnsTrue_AndRecordsTheFencingToken()
    {
        // Arrange
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        var reservationId = Guid.NewGuid();

        // Act
        var holdResult = await _eventSeatRepository.TryHoldAsync(seatId, eventId, reservationId);
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(holdResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Held, getResult.Value.Status);
        Assert.AreEqual(reservationId, getResult.Value.HeldBy);
    }

    [TestMethod]
    public async Task TryHoldAsync_WhenAlreadyHeld_ReturnsFalse_AndDoesNotThrow()
    {
        // Arrange
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, Guid.NewGuid());

        // Act
        var result = await _eventSeatRepository.TryHoldAsync(seatId, eventId, Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public async Task TryHoldAsync_WhenTwentyFiveRequestsRaceForTheSameSeat_ExactlyOneSucceeds()
    {
        // Arrange - the money test: proves the WHERE status = 'Available'
        // CAS is genuinely atomic under real concurrent load, not just
        // correct on paper. Each contender carries its own fencing token,
        // exactly as separate concurrent reservation attempts would.
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        var reservationIds = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()).ToArray();

        // Act
        var results = await Task.WhenAll(
            reservationIds.Select(reservationId =>
                _eventSeatRepository.TryHoldAsync(seatId, eventId, reservationId)));

        // Assert - exactly one wins, no exceptions, no double-hold
        Assert.IsTrue(results.All(r => r.IsSuccess));
        Assert.AreEqual(1, results.Count(r => r.Value));

        var finalState = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
        Assert.IsNotNull(finalState.Value);
        Assert.AreEqual(EventSeatStatus.Held, finalState.Value.Status);
        CollectionAssert.Contains(reservationIds, finalState.Value.HeldBy!.Value);
    }

    // ============================================================
    // TryReserveAsync
    // ============================================================

    [TestMethod]
    public async Task TryReserveAsync_WhenHeldByThisReservation_ReturnsTrue()
    {
        // Arrange
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        var reservationId = Guid.NewGuid();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, reservationId);

        // Act
        var reserveResult = await _eventSeatRepository.TryReserveAsync(seatId, eventId, reservationId);
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(reserveResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Reserved, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryReserveAsync_WhenHeldByADifferentReservation_ReturnsFalse()
    {
        // Arrange - the fencing check doing its job: status alone says
        // Held, but it isn't this caller's hold to promote
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, Guid.NewGuid());

        // Act
        var result = await _eventSeatRepository.TryReserveAsync(seatId, eventId, Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public async Task TryReserveAsync_WhenAvailable_ReturnsFalse()
    {
        // Arrange
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();

        // Act
        var result = await _eventSeatRepository.TryReserveAsync(seatId, eventId, Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryReleaseAsync
    // ============================================================

    [TestMethod]
    public async Task TryReleaseAsync_WhenHeldByThisReservation_ReturnsTrue_AndClearsTheFencingToken()
    {
        // Arrange
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        var reservationId = Guid.NewGuid();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, reservationId);

        // Act
        var releaseResult = await _eventSeatRepository.TryReleaseAsync(seatId, eventId, reservationId);
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(releaseResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Available, getResult.Value.Status);
        Assert.IsNull(getResult.Value.HeldBy);
    }

    [TestMethod]
    public async Task TryReleaseAsync_WhenReservedByThisReservation_ReturnsTrue_AndPersistsAvailableStatus()
    {
        // Arrange - proves the release predicate doesn't care whether the
        // seat was Held or Reserved, only who owns it
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        var reservationId = Guid.NewGuid();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, reservationId);
        await _eventSeatRepository.TryReserveAsync(seatId, eventId, reservationId);

        // Act
        var releaseResult = await _eventSeatRepository.TryReleaseAsync(seatId, eventId, reservationId);
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(releaseResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Available, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryReleaseAsync_WhenHeldByADifferentReservation_ReturnsFalse_AndDoesNotReleaseIt()
    {
        // Arrange - the exact bug the fencing token exists to prevent: a
        // stale caller must not be able to release someone else's hold
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        await _eventSeatRepository.TryHoldAsync(seatId, eventId, Guid.NewGuid());

        // Act
        var result = await _eventSeatRepository.TryReleaseAsync(seatId, eventId, Guid.NewGuid());
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Held, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryReleaseAsync_WhenAlreadyAvailable_ReturnsFalse()
    {
        // Arrange
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();

        // Act
        var result = await _eventSeatRepository.TryReleaseAsync(seatId, eventId, Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryHoldAsync — event-status gate
    // ============================================================

    [TestMethod]
    public async Task TryHoldAsync_WhenEventIsStillDraft_ReturnsFalse()
    {
        // Arrange - a row can exist (via a direct ProvisionAsync call,
        // or a Publish attempt that later lost its own pivot) without the
        // event ever actually reaching Published. The hold must still refuse.
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var provisionResult = await _eventSeatRepository.ProvisionAsync(eventId, venueId);
        Assert.IsTrue(provisionResult.IsSuccess, string.Join("; ", provisionResult.ToFailureMessages()));
        // Deliberately not calling _eventRepository.TryPublishAsync - the event stays Draft

        // Act
        var result = await _eventSeatRepository.TryHoldAsync(seatId, eventId, Guid.NewGuid());
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Available, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryHoldAsync_WhenEventWasCancelledAfterProvisioning_ReturnsFalse()
    {
        // Arrange - the leftover-rows scenario. Publish provisions and
        // commits, then the event is cancelled - any still-Available rows
        // must become permanently unholdable, with no extra cleanup needed.
        var (seatId, eventId, _) = await SeedAvailableEventSeatAsync();
        await _eventRepository.TryCancelAsync(eventId);

        // Act
        var result = await _eventSeatRepository.TryHoldAsync(seatId, eventId, Guid.NewGuid());
        var getResult = await _eventSeatRepository.GetByIdAsync(seatId, eventId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventSeatStatus.Available, getResult.Value.Status);
    }
}