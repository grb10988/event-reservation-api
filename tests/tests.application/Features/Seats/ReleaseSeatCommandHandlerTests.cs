using EventReservation.Application.Features.Seats;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Seats;

[TestClass]
public class ReleaseSeatCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();

    private ISeatRepository _seatRepository = null!;
    private ReleaseSeatCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _seatRepository = Substitute.For<ISeatRepository>();
        _handler = new ReleaseSeatCommandHandler(_seatRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsHeld_ReturnsSuccessAndCallsTryReleaseAsync()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryReleaseAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ReleaseSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(seat.Id, result.Value.SeatId);
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsReserved_ReturnsSuccessAndCallsTryReleaseAsync()
    {
        // Arrange - Release is valid from either Held or Reserved
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        seat.Hold();
        seat.Reserve();

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryReleaseAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new ReleaseSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatIsAvailable_ReturnsFailureWithCannotReleaseError_AndNeverCallsTryReleaseAsync()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));

        // Act
        var result = await _handler.HandleAsync(new ReleaseSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Seat.Errors.CannotRelease);
        await _seatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryReleaseAsyncLosesTheRace_ReturnsFailureWithSeatNotHeldOrReservedError()
    {
        // Arrange
        var seat = Seat.Create(VenueId, "A", 1, 1).Value;
        Assert.IsNotNull(seat);
        seat.Hold();

        _seatRepository.GetByIdAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(seat));
        _seatRepository.TryReleaseAsync(seat.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new ReleaseSeatCommand(seat.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), ReleaseSeatCommandHandler.Errors.SeatNotHeldOrReserved);
    }
}