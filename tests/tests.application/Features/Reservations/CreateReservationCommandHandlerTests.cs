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

    private IEventSeatRepository _eventSeatRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CreateReservationCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventSeatRepository = Substitute.For<IEventSeatRepository>();
        _reservationRepository = Substitute.For<IReservationRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CreateReservationCommandHandler(_reservationRepository, _eventSeatRepository, _timeProvider);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenSeatHoldsAndReservationInsertSucceeds_ReturnsSuccess_AndNeverCallsTryReleaseAsync()
    {
        // Arrange
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        _eventSeatRepository.TryHoldAsync(SeatId, EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Reservation>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SeatId, result.Value.SeatId);
        Assert.AreEqual(ReservationStatus.Held, result.Value.Status);
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatHolds_UsesTheReservationIdAsTheFencingToken()
    {
        // Arrange
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        _eventSeatRepository.TryHoldAsync(SeatId, EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Reservation>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _eventSeatRepository.Received(1).TryHoldAsync(SeatId, EventId, result.Value.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenSeatCannotBeHeld_ReturnsFailureWithSeatNotAvailableError_AndNeverCallsAddAsyncOrTryReleaseAsync()
    {
        // Arrange
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        _eventSeatRepository.TryHoldAsync(SeatId, EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CreateReservationCommandHandler.Errors.SeatNotAvailable);
        await _reservationRepository.DidNotReceive().AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>());
        await _eventSeatRepository.DidNotReceive().TryReleaseAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenInputIsInvalid_ReturnsFailure_AndNeverTouchesTheSeat()
    {
        // Arrange - validated before acquiring shared state: no hold attempt,
        // no compensating release
        var command = new CreateReservationCommand(SeatId, Guid.Empty, CustomerId, ValidPrice);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Reservation.Errors.EmptyEventId);
        await _eventSeatRepository.DidNotReceive().TryHoldAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationInsertFails_CompensatesByReleasingTheSeat_AndPropagatesTheOriginalError()
    {
        // Arrange - the money test: proves TapError's compensation actually
        // fires, releases with the same fencing token used to acquire, and
        // that it's the insert's own error that surfaces
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice);
        var insertError = new ResultError("TEST", "insert failed");
        Guid heldWith = Guid.Empty;

        _eventSeatRepository.TryHoldAsync(SeatId, EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => { heldWith = callInfo.ArgAt<Guid>(2); return Success(true); });
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>()).Returns(Failure<Reservation>(insertError));
        _eventSeatRepository.TryReleaseAsync(SeatId, EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), insertError);
        await _eventSeatRepository.Received(1).TryReleaseAsync(SeatId, EventId, heldWith, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithCustomHoldDuration_PassesItThroughToReservationCreate()
    {
        // Arrange
        var customDuration = TimeSpan.FromMinutes(30);
        var command = new CreateReservationCommand(SeatId, EventId, CustomerId, ValidPrice, customDuration);

        _eventSeatRepository.TryHoldAsync(SeatId, EventId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Success(true));
        _reservationRepository.AddAsync(Arg.Any<Reservation>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Reservation>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Now + customDuration, result.Value.HoldExpiresAt);
    }
}