using EventReservation.Application.Features.Seats;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Seats;

[TestClass]
public class HoldSeatCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();

    private ISeatRepository _seatRepository = null!;
    private HoldSeatCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _seatRepository = Substitute.For<ISeatRepository>();
        _handler = new HoldSeatCommandHandler(_seatRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsAvailable_ReturnsSuccessAndCallsTryHoldAsync()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryHoldAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new HoldSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(seat.Id, result.Value.SeatId);
        await _seatRepository.Received(1).TryHoldAsync(seat.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsAlreadyHeld_ReturnsFailureWithCannotHoldError_AndNeverCallsTryHoldAsync()
    {
        // Arrange - domain state, not repository state, is what should block this
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));

        // Act
        var result = await _handler.HandleAsync(new HoldSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotHold);
        await _seatRepository.DidNotReceive().TryHoldAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryHoldAsyncLosesTheRace_ReturnsFailureWithSeatNotAvailableError()
    {
        // Arrange - passes the in-memory domain check, but the atomic DB call reports it lost
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryHoldAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new HoldSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), HoldSeatCommandHandler.Errors.SeatNotAvailable);
    }

    [TestMethod]
    public async Task HandleAsync_WhenGetByIdAsyncFails_PropagatesFailure_AndNeverCallsTryHoldAsync()
    {
        // Arrange
        var seatId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _seatRepository.GetByIdAsync(seatId, Arg.Any<CancellationToken>()).Returns(Failure<Seat>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new HoldSeatCommand(seatId));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
        await _seatRepository.DidNotReceive().TryHoldAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}