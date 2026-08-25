using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Reservations;

[TestClass]
public class CancelReservationCommandHandlerTests
{
    private static readonly Guid SeatId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IReservationRepository _reservationRepository = null!;
    private ISeatRepository _seatRepository = null!;
    private CancelReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _seatRepository = Substitute.For<ISeatRepository>();
        _handler = new CancelReservationCommandHandler(_reservationRepository, _seatRepository);
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
        _reservationRepository.TryCancelAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _seatRepository.TryReleaseAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CancelReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _seatRepository.Received(1).TryReleaseAsync(SeatId, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenConfirmed_ReturnsSuccess_AndCallsTryReleaseAsyncOnTheSeat()
    {
        // Arrange - Cancel is valid from either Held or Confirmed
        var reservation = CreateHeldReservation();
        reservation.Confirm();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryCancelAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _seatRepository.TryReleaseAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CancelReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _seatRepository.Received(1).TryReleaseAsync(SeatId, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenExpired_ReturnsFailureWithCannotCancelError_AndNeverTouchesTheSeat()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        reservation.Expire();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

        // Act
        var result = await _handler.HandleAsync(new CancelReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.CannotCancel);
        await _seatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryCancelAsyncLosesTheRace_ReturnsFailure_AndNeverCallsTryReleaseAsync()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryCancelAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CancelReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CancelReservationCommandHandler.Errors.ReservationNotHeldOrConfirmed);
        await _seatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationCancelsButSeatCannotBeReleased_ReturnsFailureWithSeatCouldNotBeReleasedError()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryCancelAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _seatRepository.TryReleaseAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CancelReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CancelReservationCommandHandler.Errors.SeatCouldNotBeReleased);
    }
}