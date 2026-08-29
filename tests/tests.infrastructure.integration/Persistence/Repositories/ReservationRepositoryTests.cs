using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class ReservationRepositoryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IVenueRepository _venueRepository = null!;
    private ISeatRepository _seatRepository = null!;
    private IEventRepository _eventRepository = null!;
    private ICustomerRepository _customerRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private FakeTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = new VenueRepository(ConnectionFactory);
        _seatRepository = new SeatRepository(ConnectionFactory);
        _eventRepository = new EventRepository(ConnectionFactory);
        _customerRepository = new CustomerRepository(ConnectionFactory);
        _reservationRepository = new ReservationRepository(ConnectionFactory);
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

    private async Task<Guid> SeedCustomerAsync(string email = "jane.doe@example.com")
    {
        var customer = Customer.Create("Jane", "Doe", email).Value;
        Assert.IsNotNull(customer);
        var result = await _customerRepository.AddAsync(customer);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Reservation> SeedHeldReservationAsync(Guid seatId, Guid eventId, Guid customerId, decimal price = 50.00m, TimeSpan? holdDuration = null)
    {
        var reservation = Reservation.Create(seatId, eventId, customerId, price, _timeProvider, holdDuration).Value;
        Assert.IsNotNull(reservation);
        var result = await _reservationRepository.AddAsync(reservation);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value;
    }

    // ============================================================
    // AddAsync / GetByIdAsync
    // ============================================================

    [TestMethod]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFieldsCorrectly()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = Reservation.Create(seatId, eventId, customerId, 65.00m, _timeProvider).Value;
        Assert.IsNotNull(reservation);

        // Act
        var addResult = await _reservationRepository.AddAsync(reservation);
        var getResult = await _reservationRepository.GetByIdAsync(reservation.Id);

        // Assert
        Assert.IsTrue(addResult.IsSuccess, string.Join("; ", addResult.ToFailureMessages()));
        Assert.IsTrue(getResult.IsSuccess, string.Join("; ", getResult.ToFailureMessages()));
        Assert.AreEqual(reservation.Id, getResult.Value.Id);
        Assert.AreEqual(seatId, getResult.Value.SeatId);
        Assert.AreEqual(eventId, getResult.Value.EventId);
        Assert.AreEqual(customerId, getResult.Value.CustomerId);
        Assert.AreEqual(65.00m, getResult.Value.Price);
        Assert.AreEqual(ReservationStatus.Held, getResult.Value.Status);
        Assert.AreEqual(reservation.CreatedAt, getResult.Value.CreatedAt);
        Assert.AreEqual(reservation.HoldExpiresAt, getResult.Value.HoldExpiresAt);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenReservationDoesNotExist_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _reservationRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // AddAsync - partial unique index (ux_reservations_active_seat_event)
    // ============================================================

    [TestMethod]
    public async Task AddAsync_WithSecondActiveReservationForSameSeatAndEvent_ReturnsDuplicateRecordFailure()
    {
        // Arrange - this is the money test for Reservation: proves the
        // partial unique index actually blocks a second Held/Confirmed
        // reservation for the same seat+event, at the database level
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId1 = await SeedCustomerAsync("jane.doe@example.com");
        var customerId2 = await SeedCustomerAsync("john.smith@example.com");

        await SeedHeldReservationAsync(seatId, eventId, customerId1);
        var secondReservation = Reservation.Create(seatId, eventId, customerId2, 50.00m, _timeProvider).Value;
        Assert.IsNotNull(secondReservation);

        // Act
        var result = await _reservationRepository.AddAsync(secondReservation);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), DatabaseExceptionMapper.Errors.DuplicateRecord);
    }

    [TestMethod]
    public async Task AddAsync_WhenPriorReservationForSameSeatAndEventWasCancelled_Succeeds()
    {
        // Arrange - proves the index is partial: it only restricts Held/
        // Confirmed rows, so a Cancelled reservation doesn't block a new one
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId1 = await SeedCustomerAsync("jane.doe@example.com");
        var customerId2 = await SeedCustomerAsync("john.smith@example.com");

        var firstReservation = await SeedHeldReservationAsync(seatId, eventId, customerId1);
        await _reservationRepository.TryCancelAsync(firstReservation.Id);

        var secondReservation = Reservation.Create(seatId, eventId, customerId2, 50.00m, _timeProvider).Value;
        Assert.IsNotNull(secondReservation);

        // Act
        var result = await _reservationRepository.AddAsync(secondReservation);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
    }

    // ============================================================
    // GetByCustomerIdAsync
    // ============================================================

    [TestMethod]
    public async Task GetByCustomerIdAsync_ReturnsOnlyReservationsForThatCustomer()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var eventId = await SeedEventAsync(venueId);
        var customerId1 = await SeedCustomerAsync("jane.doe@example.com");
        var customerId2 = await SeedCustomerAsync("john.smith@example.com");

        var ownReservation = await SeedHeldReservationAsync(seatId1, eventId, customerId1);
        await SeedHeldReservationAsync(seatId2, eventId, customerId2);

        // Act
        var result = await _reservationRepository.GetByCustomerIdAsync(customerId1);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.HasCount(1, result.Value);
        Assert.AreEqual(ownReservation.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task GetByCustomerIdAsync_WhenNoReservationsExist_ReturnsEmptyList()
    {
        // Arrange
        var customerId = await SeedCustomerAsync();

        // Act
        var result = await _reservationRepository.GetByCustomerIdAsync(customerId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsEmpty(result.Value);
    }

    // ============================================================
    // GetExpiredHoldsAsync
    // ============================================================

    [TestMethod]
    public async Task GetExpiredHoldsAsync_ReturnsOnlyHeldReservationsPastTheGivenTime()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var seatId3 = await SeedSeatAsync(venueId, "A", 1, 3);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();

        // Expires soon - should be returned
        var expiringSoon = await SeedHeldReservationAsync(seatId1, eventId, customerId, holdDuration: TimeSpan.FromMinutes(1));

        // Expires far in the future - should not be returned
        await SeedHeldReservationAsync(seatId2, eventId, customerId, holdDuration: TimeSpan.FromMinutes(60));

        // Would be time-expired, but is Confirmed, not Held - should not be returned
        var confirmed = await SeedHeldReservationAsync(seatId3, eventId, customerId, holdDuration: TimeSpan.FromMinutes(1));
        await _reservationRepository.TryConfirmAsync(confirmed.Id);

        // Act
        var result = await _reservationRepository.GetExpiredHoldsAsync(Now.AddMinutes(30));

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.HasCount(1, result.Value);
        Assert.AreEqual(expiringSoon.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task GetExpiredHoldsAsync_WhenNothingHasExpired_ReturnsEmptyList()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        await SeedHeldReservationAsync(seatId, eventId, customerId, holdDuration: TimeSpan.FromMinutes(60));

        // Act
        var result = await _reservationRepository.GetExpiredHoldsAsync(Now.AddMinutes(1));

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsEmpty(result.Value);
    }

    // ============================================================
    // TryConfirmAsync
    // ============================================================

    [TestMethod]
    public async Task TryConfirmAsync_WhenHeld_ReturnsTrueAndPersistsConfirmedStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);

        // Act
        var confirmResult = await _reservationRepository.TryConfirmAsync(reservation.Id);
        var getResult = await _reservationRepository.GetByIdAsync(reservation.Id);

        // Assert
        Assert.IsTrue(confirmResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(ReservationStatus.Confirmed, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryConfirmAsync_WhenAlreadyConfirmed_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);
        await _reservationRepository.TryConfirmAsync(reservation.Id);

        // Act
        var result = await _reservationRepository.TryConfirmAsync(reservation.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryCancelAsync
    // ============================================================

    [TestMethod]
    public async Task TryCancelAsync_WhenHeld_ReturnsTrueAndPersistsCancelledStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);

        // Act
        var cancelResult = await _reservationRepository.TryCancelAsync(reservation.Id);
        var getResult = await _reservationRepository.GetByIdAsync(reservation.Id);

        // Assert
        Assert.IsTrue(cancelResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(ReservationStatus.Cancelled, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryCancelAsync_WhenConfirmed_ReturnsTrueAndPersistsCancelledStatus()
    {
        // Arrange - Cancel is valid from either Held or Confirmed
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);
        await _reservationRepository.TryConfirmAsync(reservation.Id);

        // Act
        var cancelResult = await _reservationRepository.TryCancelAsync(reservation.Id);
        var getResult = await _reservationRepository.GetByIdAsync(reservation.Id);

        // Assert
        Assert.IsTrue(cancelResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(ReservationStatus.Cancelled, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryCancelAsync_WhenAlreadyCancelled_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);
        await _reservationRepository.TryCancelAsync(reservation.Id);

        // Act
        var result = await _reservationRepository.TryCancelAsync(reservation.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryExpireAsync
    // ============================================================

    [TestMethod]
    public async Task TryExpireAsync_WhenHeld_ReturnsTrueAndPersistsExpiredStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);

        // Act
        var expireResult = await _reservationRepository.TryExpireAsync(reservation.Id);
        var getResult = await _reservationRepository.GetByIdAsync(reservation.Id);

        // Assert
        Assert.IsTrue(expireResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(ReservationStatus.Expired, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryExpireAsync_WhenConfirmed_ReturnsFalse()
    {
        // Arrange - Expire is only valid from Held, unlike Cancel
        var venueId = await SeedVenueAsync();
        var seatId = await SeedSeatAsync(venueId);
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync();
        var reservation = await SeedHeldReservationAsync(seatId, eventId, customerId);
        await _reservationRepository.TryConfirmAsync(reservation.Id);

        // Act
        var result = await _reservationRepository.TryExpireAsync(reservation.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }
}