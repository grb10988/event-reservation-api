using EventReservation.Application.Features.Seats;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Seats;

[TestClass]
public class ReserveSeatCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();

    private ISeatRepository _seatRepository = null!;
    private ReserveSeatCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _seatRepository = Substitute.For<ISeatRepository>();
        _handler = new ReserveSeatCommandHandler(_seatRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsHeld_ReturnsSuccessAndCallsTryReserveAsync()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryReserveAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ReserveSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(seat.Id, result.Value.SeatId);
        await _seatRepository.Received(1).TryReserveAsync(seat.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsAvailable_ReturnsFailureWithCannotReserveError_AndNeverCallsTryReserveAsync()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));

        // Act
        var result = await _handler.HandleAsync(new ReserveSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotReserve);
        await _seatRepository.DidNotReceive().TryReserveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryReserveAsyncLosesTheRace_ReturnsFailureWithSeatNotHeldError()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryReserveAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ReserveSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ReserveSeatCommandHandler.Errors.SeatNotHeld);
    }
}