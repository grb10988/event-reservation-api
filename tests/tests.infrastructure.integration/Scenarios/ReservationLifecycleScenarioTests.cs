using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Events;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EventReservation.Tests.Infrastructure.Integration.Scenarios;

[TestClass]
public class ReservationLifecycleScenarioTests : ScenarioTestBase
{
    private IVenueRepository _venueRepository = null!;
    private ISeatRepository _seatRepository = null!;
    private IEventRepository _eventRepository = null!;
    private ICustomerRepository _customerRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private IDispatcher _dispatcher = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = Services.GetRequiredService<IVenueRepository>();
        _seatRepository = Services.GetRequiredService<ISeatRepository>();
        _eventRepository = Services.GetRequiredService<IEventRepository>();
        _customerRepository = Services.GetRequiredService<ICustomerRepository>();
        _reservationRepository = Services.GetRequiredService<IReservationRepository>();
        _eventSeatRepository = Services.GetRequiredService<IEventSeatRepository>();
        _dispatcher = Services.GetRequiredService<IDispatcher>();
    }

    private async Task<(Guid ReservationId, Guid SeatId, Guid EventId)> SeedHeldReservationAsync(TimeSpan? holdDuration = null)
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var venueResult = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(venueResult.IsSuccess, string.Join("; ", venueResult.ToFailureMessages()));

        var seat = Seat.Create(venue.Id, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        var seatResult = await _seatRepository.AddAsync(seat);
        Assert.IsTrue(seatResult.IsSuccess, string.Join("; ", seatResult.ToFailureMessages()));

        var evt = Event.Create(venue.Id, "Summer Concert", "An outdoor concert.", Clock.GetUtcNow().AddDays(30), Clock.GetUtcNow().AddDays(30).AddHours(3), 50.00m, Clock).Value;
        Assert.IsNotNull(evt);
        var eventResult = await _eventRepository.AddAsync(evt);
        Assert.IsTrue(eventResult.IsSuccess, string.Join("; ", eventResult.ToFailureMessages()));
        var publishResult = await _dispatcher.SendAsync<PublishEventResult>(new PublishEventCommand(evt.Id));
        Assert.IsTrue(publishResult.IsSuccess, string.Join("; ", publishResult.ToFailureMessages()));

        var customer = Customer.Create("Jane", "Doe", $"jane.{Guid.NewGuid()}@example.com").Value;
        Assert.IsNotNull(customer);
        var customerResult = await _customerRepository.AddAsync(customer);
        Assert.IsTrue(customerResult.IsSuccess, string.Join("; ", customerResult.ToFailureMessages()));

        var createResult = await _dispatcher.SendAsync<CreateReservationResult>(
            new CreateReservationCommand(seat.Id, evt.Id, customer.Id, 50.00m, holdDuration));
        Assert.IsTrue(createResult.IsSuccess, string.Join("; ", createResult.ToFailureMessages()));

        return (createResult.Value.Id, seat.Id, evt.Id);
    }

    // ============================================================
    // Confirm vs. Cancel
    // ============================================================

    [TestMethod]
    public async Task ConfirmAndCancel_WhenRacedOnTheSameHeldReservation_ConvergesToOneConsistentOutcome()
    {
        // Arrange - regardless of which call actually wins the reservation's
        // own CAS, the seat must end up in the state that matches, never a
        // Reserved seat on a Cancelled reservation
        var (reservationId, seatId, eventId) = await SeedHeldReservationAsync();

        // Act - fire both concurrently, then re-await each individually for
        // its real typed result; Task.WhenAll here is only a sync barrier -
        // it can't return a typed array since the two tasks have different
        // TResponse types
        var confirmTask = _dispatcher.SendAsync<ConfirmReservationResult>(new ConfirmReservationCommand(reservationId));
        var cancelTask = _dispatcher.SendAsync<CancelReservationResult>(new CancelReservationCommand(reservationId));
        await Task.WhenAll(confirmTask, cancelTask);
        var confirmResult = await confirmTask;
        var cancelResult = await cancelTask;

        // Assert - exactly one of the two actually won the reservation's CAS
        Assert.AreNotEqual(confirmResult.IsSuccess, cancelResult.IsSuccess);

        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        var seat = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
        Assert.IsNotNull(reservation.Value);
        Assert.IsNotNull(seat.Value);

        if (confirmResult.IsSuccess)
        {
            Assert.AreEqual(ReservationStatus.Confirmed, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Reserved, seat.Value.Status);
        }
        else
        {
            Assert.AreEqual(ReservationStatus.Cancelled, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Available, seat.Value.Status);
        }
    }

    // ============================================================
    // Confirm vs. Expire
    // ============================================================

    [TestMethod]
    public async Task ConfirmAndExpire_WhenRacedOnTheSameHeldReservation_ConvergesToOneConsistentOutcome()
    {
        // Arrange - same convergence proof as the Confirm-vs-Cancel race
        // above, with Expired replacing Cancelled as the losing outcome
        var (reservationId, seatId, eventId) = await SeedHeldReservationAsync();

        // Act
        var confirmTask = _dispatcher.SendAsync<ConfirmReservationResult>(new ConfirmReservationCommand(reservationId));
        var expireTask = _dispatcher.SendAsync<ExpireReservationResult>(new ExpireReservationCommand(reservationId));
        await Task.WhenAll(confirmTask, expireTask);
        var confirmResult = await confirmTask;
        var expireResult = await expireTask;

        // Assert
        Assert.AreNotEqual(confirmResult.IsSuccess, expireResult.IsSuccess);

        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        var seat = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
        Assert.IsNotNull(reservation.Value);
        Assert.IsNotNull(seat.Value);

        if (confirmResult.IsSuccess)
        {
            Assert.AreEqual(ReservationStatus.Confirmed, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Reserved, seat.Value.Status);
        }
        else
        {
            Assert.AreEqual(ReservationStatus.Expired, reservation.Value.Status);
            Assert.AreEqual(EventSeatStatus.Available, seat.Value.Status);
        }
    }

    // ============================================================
    // Duplicate Confirm
    // ============================================================

    [TestMethod]
    public async Task Confirm_WhenTwentyFiveRequestsRaceForTheSameHeldReservation_ExactlyOneSucceeds()
    {
        // Arrange: the fencing predicate (status='Held' AND
        // held_by=@ReservationId) must admit exactly one winner even
        // under real concurrent load, not just on a sequential retry
        var (reservationId, seatId, eventId) = await SeedHeldReservationAsync();

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ =>
                _dispatcher.SendAsync<ConfirmReservationResult>(new ConfirmReservationCommand(reservationId))));

        // Assert
        Assert.AreEqual(1, results.Count(r => r.IsSuccess));

        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        var seat = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
        Assert.IsNotNull(reservation.Value);
        Assert.IsNotNull(seat.Value);
        Assert.AreEqual(ReservationStatus.Confirmed, reservation.Value.Status);
        Assert.AreEqual(EventSeatStatus.Reserved, seat.Value.Status);
    }

    // ============================================================
    // Expired-hold sweep, functionally
    // ============================================================

    [TestMethod]
    public async Task ExpiredHoldSweep_WhenAHeldReservationIsPastItsExpiry_GetsExpiredAndReleasesItsSeat()
    {
        // Arrange - exercises exactly what ReservationHoldSweepWorker's
        // SweepOnceAsync does on a real tick (query, then dispatch Expire
        // for each result), without needing the BackgroundService's own
        // timer/scope machinery - that wrapper has no logic of its own to
        // prove, same as RabbitMqConsumerWorker is never tested directly
        var (reservationId, seatId, eventId) = await SeedHeldReservationAsync(TimeSpan.FromMinutes(1));
        Clock.Advance(TimeSpan.FromMinutes(2));

        // Act
        var expired = await _reservationRepository.GetExpiredHoldsAsync(Clock.GetUtcNow());
        Assert.IsTrue(expired.IsSuccess, string.Join("; ", expired.ToFailureMessages()));
        Assert.HasCount(1, expired.Value);

        var expireResults = await Task.WhenAll(expired.Value.Select(r =>
            _dispatcher.SendAsync<ExpireReservationResult>(new ExpireReservationCommand(r.Id))));

        // Assert
        Assert.IsTrue(expireResults.All(r => r.IsSuccess));

        var reservation = await _reservationRepository.GetByIdAsync(reservationId);
        var seat = await _eventSeatRepository.GetByIdAsync(seatId, eventId);
        Assert.IsNotNull(reservation.Value);
        Assert.IsNotNull(seat.Value);
        Assert.AreEqual(ReservationStatus.Expired, reservation.Value.Status);
        Assert.AreEqual(EventSeatStatus.Available, seat.Value.Status);
    }
}