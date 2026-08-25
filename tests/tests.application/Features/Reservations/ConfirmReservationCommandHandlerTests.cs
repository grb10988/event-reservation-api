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
    private ISeatRepository _seatRepository = null!;
    private ConfirmReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _seatRepository = Substitute.For<ISeatRepository>();
        _handler = new ConfirmReservationCommandHandler(_reservationRepository, _seatRepository);
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
    public async Task HandleAsync_WhenHeld_ReturnsSuccess_AndCallsTryReserveAsyncOnTheSeat()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryConfirmAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _seatRepository.TryReserveAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _seatRepository.Received(1).TryReserveAsync(SeatId, Arg.Any<CancellationToken>());
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
        await _seatRepository.DidNotReceive().TryReserveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryConfirmAsyncLosesTheRace_ReturnsFailure_AndNeverCallsTryReserveAsync()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryConfirmAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ConfirmReservationCommandHandler.Errors.ReservationNotHeld);
        await _seatRepository.DidNotReceive().TryReserveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationConfirmsButSeatCannotBeReserved_ReturnsFailureWithSeatCouldNotBeReservedError()
    {
        // Arrange - the two aggregates disagreeing is exactly the drift
        // this cascade exists to prevent; confirm the handler reports it
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryConfirmAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _seatRepository.TryReserveAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ConfirmReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ConfirmReservationCommandHandler.Errors.SeatCouldNotBeReserved);
    }
}