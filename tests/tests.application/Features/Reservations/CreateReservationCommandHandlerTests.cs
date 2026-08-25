using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Reservations;

[TestClass]
public class CreateReservationCommandHandlerTests
{
    private static readonly Guid SeatId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private const decimal ValidPrice = 50.00m;
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private ISeatRepository _seatRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CreateReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _seatRepository = Substitute.For<ISeatRepository>();
        _reservationRepository = Substitute.For<IReservationRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CreateReservationCommandHandler(_reservationRepository, _seatRepository, _timeProvider);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenSeatHoldsAndReservationInsertSucceeds_ReturnsSuccess_AndNeverCallsTryReleaseAsync()
    {
        // Arrange
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        _seatRepository.TryHoldAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Reservation>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SeatId, result.Value.SeatId);
        Assert.AreEqual(ReservationStatus.Held, result.Value.Status);
        await _seatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatCannotBeHeld_ReturnsFailureWithSeatNotAvailableError_AndNeverCallsAddAsync()
    {
        // Arrange
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        _seatRepository.TryHoldAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CreateReservationCommandHandler.Errors.SeatNotAvailable);
        await _reservationRepository.DidNotReceive().AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationInsertFails_CompensatesByReleasingTheSeat_AndPropagatesTheOriginalError()
    {
        // Arrange - this is the money test: proves TapError's compensation
        // actually fires, and that it's the insert's own error that surfaces,
        // not something masked by the compensating release call
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        var insertError = new ResultError("TEST", "insert failed");

        _seatRepository.TryHoldAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>()).Returns(Failure<Reservation>(insertError));
        _seatRepository.TryReleaseAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), insertError);
        await _seatRepository.Received(1).TryReleaseAsync(SeatId, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithCustomHoldDuration_PassesItThroughToReservationCreate()
    {
        // Arrange
        var customDuration = TimeSpan.FromMinutes(30);
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice, customDuration);

        _seatRepository.TryHoldAsync(SeatId, Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Reservation>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Now + customDuration, result.Value.HoldExpiresAt);
    }
}