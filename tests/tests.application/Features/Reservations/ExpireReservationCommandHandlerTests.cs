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
    private ISeatRepository _seatRepository = null!;
    private ExpireReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _reservationRepository = Substitute.For<IReservationRepository>();
        _seatRepository = Substitute.For<ISeatRepository>();
        _handler = new ExpireReservationCommandHandler(_reservationRepository, _seatRepository);
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
        _seatRepository.TryReleaseAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _seatRepository.Received(1).TryReleaseAsync(SeatId, Arg.Any<CancellationToken>());
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
        await _seatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
        await _seatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationExpiresButSeatCannotBeReleased_ReturnsFailureWithSeatCouldNotBeReleasedError()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));
        _reservationRepository.TryExpireAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        _seatRepository.TryReleaseAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ExpireReservationCommand(reservation.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ExpireReservationCommandHandler.Errors.SeatCouldNotBeReleased);
    }
}