using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Reservations;

[TestClass]
public class ConfirmReservationCommandHandlerTests
{
    private static readonly Guid SeatId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReservationRepository _reservationRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private ConfirmReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _eventSeatRepository = Substitute.For<IEventSeatRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new ConfirmReservationCommandHandler(_reservationRepository, _eventSeatRepository, _timeProvider);
    }

    private Reservation CreateHeldReservation() =>
        Reservation.Create(SeatId, EventId, CustomerId, 50.00m, _timeProvider).Value!;

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenHeld_ReservesTheSeatBeforeConfirmingTheReservation_AndReturnsSuccess()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _eventSeatRepository.TryReserveAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.TryConfirmAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Received.InOrder(() =>
        {
            _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>());
            _eventSeatRepository.TryReserveAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>());
            _reservationRepository.TryConfirmAsync(reservation.Id, Arg.Any<CancellationToken>());
        });
    }

    [TestMethod]
    public async Task HandleAsync_WhenNotHeld_ReturnsFailureWithCannotConfirmError_AndNeverTouchesTheSeat()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        reservation.Cancel();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotConfirm);
        await _eventSeatRepository.DidNotReceive().TryReserveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenHoldHasExpired_ReturnsFailureWithHoldExpiredError_AndNeverTouchesTheSeat()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _timeProvider.Advance(Reservation.DefaultHoldDuration + TimeSpan.FromSeconds(1));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ConfirmReservationCommandHandler.Errors.HoldExpired);
        await _eventSeatRepository.DidNotReceive().TryReserveAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _reservationRepository.DidNotReceive().TryConfirmAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsNoLongerHeldByThisReservation_ReturnsFailure_AndNeverCallsTryConfirmAsync()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _eventSeatRepository.TryReserveAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ConfirmReservationCommandHandler.Errors.SeatNotHeldByReservation);
        await _reservationRepository.DidNotReceive().TryConfirmAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryConfirmAsyncLosesTheRace_ReturnsFailure_AndNeverReleasesTheSeat()
    {
        // Arrange - whoever won the reservation (Cancel/Expire, or a
        // duplicate Confirm) owns the seat cleanup; releasing here would be
        // exactly the bug the fencing token exists to prevent
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _eventSeatRepository.TryReserveAsync(SeatId, EventId, reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.TryConfirmAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ConfirmReservationCommandHandler.Errors.ReservationNotHeld);
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}