using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Reservations;

[TestClass]
public class ExpireReservationCommandHandlerTests
{
    private static readonly Guid SeatId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReservationRepository _reservationRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private ExpireReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _eventSeatRepository = Substitute.For<IEventSeatRepository>();
        _handler = new ExpireReservationCommandHandler(_reservationRepository, _eventSeatRepository);
    }

    private Reservation CreateHeldReservation()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var reservation = Reservation.Create(SeatId, EventId, CustomerId, 50.00m, timeProvider).Value;
        Assert.IsNotNull(reservation);
        return reservation;
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenHeld_ReturnsSuccess_AndCallsTryReleaseAsyncOnTheSeat()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryExpireAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _eventSeatRepository.TryReleaseAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _eventSeatRepository.Received(1).TryReleaseAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenConfirmed_ReturnsFailureWithCannotExpireError_AndNeverTouchesTheSeat()
    {
        // Arrange - Expire is only valid from Held, unlike Cancel
        var reservation = CreateHeldReservation();
        reservation.Confirm();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotExpire);
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryExpireAsyncLosesTheRace_ReturnsFailure_AndNeverCallsTryReleaseAsync()
    {
        // Arrange - e.g. the customer confirmed at the same instant the
        // expiration worker tried to expire it
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryExpireAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ExpireReservationCommandHandler.Errors.ReservationNotHeld);
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsAlreadyReleasedOrSuperseded_StillReturnsSuccess()
    {
        // Arrange - the fenced release is idempotent
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryExpireAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _eventSeatRepository.TryReleaseAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatReleaseFailsInfrastructurally_SurfacesTheFailure()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        var infraError = new ResultError("TEST", "database unavailable", ErrorCategory.Unavailable);
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryExpireAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _eventSeatRepository.TryReleaseAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>()).Returns(Failure<bool>(infraError));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), infraError);
    }

    [TestMethod]
    public async Task HandleAsync_WhenAlreadyExpired_ReturnsSuccess_AsAnIdempotentNoOp()
    {
        // Arrange - a retried sweep tick (or a stale caller) landing on a
        // reservation that's already Expired should see success, not an error
        var reservation = CreateHeldReservation();
        reservation.Expire();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _reservationRepository.DidNotReceive().TryExpireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenAlreadyCancelled_ReturnsSuccess_AsAnIdempotentNoOp()
    {
        // Arrange - the sweep tried to expire a hold that a customer had
        // already cancelled themselves; same terminal-state tolerance
        var reservation = CreateHeldReservation();
        reservation.Cancel();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _reservationRepository.DidNotReceive().TryExpireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}