using EventReservation.Application.Features.Events;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class UpdateEventCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidStartTime = Now.AddDays(30);
    private static readonly DateTimeOffset ValidEndTime = ValidStartTime.AddHours(3);

    private IEventRepository _eventRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private UpdateEventCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new UpdateEventCommandHandler(_eventRepository, _timeProvider);
    }

    private Event CreateValidEvent()
    {
        var evt = Event.Create(VenueId, "Summer Concert", "An outdoor concert.", ValidStartTime, ValidEndTime, 50.00m, _timeProvider).Value;
        Assert.IsNotNull(evt);
        return evt;
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessAndCallsUpdateAsync()
    {
        // Arrange
        var evt = CreateValidEvent();
        var command = new UpdateEventCommand(evt.Id, "Winter Gala", "A rescheduled gala.", ValidStartTime.AddDays(5), ValidStartTime.AddDays(5).AddHours(2), 75.00m);

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Event>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(command.Name, result.Value.Name);
        Assert.AreEqual(command.TicketPrice, result.Value.TicketPrice);
        await _eventRepository.Received(1).UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithMultipleInvalidFields_AccumulatesAllErrors_AndNeverCallsUpdateAsync()
    {
        // Arrange - proves Combine's accumulation survives all the way through
        // the handler, not just within Domain's own unit tests
        var evt = CreateValidEvent();
        var command = new UpdateEventCommand(evt.Id, "", "", Now.AddDays(-1), Now.AddDays(-2), -5m);

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyName);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyDescription);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidTicketPrice);
        await _eventRepository.DidNotReceive().UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenEventNotFound_PropagatesFailure_AndNeverCallsUpdateAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _eventRepository.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(Failure<Event>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new UpdateEventCommand(eventId, "Winter Gala", "desc", ValidStartTime, ValidEndTime, 50m));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
        await _eventRepository.DidNotReceive().UpdateAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }
}