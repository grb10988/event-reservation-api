using EventReservation.Application.Features.Events;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class PublishEventCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IEventRepository _eventRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private PublishEventCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _eventSeatRepository = Substitute.For<IEventSeatRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new PublishEventCommandHandler(_eventRepository, _eventSeatRepository);
    }

    private Event CreateDraftEvent()
    {
        var evt = Event.Create(VenueId, "Summer Concert", "An outdoor concert.", Now.AddDays(30), Now.AddDays(30).AddHours(3), 50.00m, _timeProvider).Value;
        Assert.IsNotNull(evt);
        return evt;
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenDraft_ProvisionsSeatsBeforePublishing_ReturnsSuccess()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventSeatRepository.ProvisionAsync(evt.Id, evt.VenueId, Arg.Any<CancellationToken>()).Returns(Success());
        _eventRepository.TryPublishAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new PublishEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Received.InOrder(() =>
        {
            _eventSeatRepository.ProvisionAsync(evt.Id, evt.VenueId, Arg.Any<CancellationToken>());
            _eventRepository.TryPublishAsync(evt.Id, Arg.Any<CancellationToken>());
        });
    }

    [TestMethod]
    public async Task HandleAsync_WhenAlreadyPublished_ReturnsFailureWithCannotPublishError_AndNeverTouchesEventSeats()
    {
        // Arrange
        var evt = CreateDraftEvent();
        evt.Publish();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));

        // Act
        var result = await _handler.HandleAsync(new PublishEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotPublish);
        await _eventSeatRepository.DidNotReceive().ProvisionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _eventRepository.DidNotReceive().TryPublishAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenProvisioningFails_ReturnsFailure_AndNeverCallsTryPublishAsync()
    {
        // Arrange - the pre-pivot step failing must stop before the pivot,
        // leaving the event genuinely still Draft
        var evt = CreateDraftEvent();
        var provisionError = new ResultError("TEST", "database unavailable", ErrorCategory.Unavailable);
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventSeatRepository.ProvisionAsync(evt.Id, evt.VenueId, Arg.Any<CancellationToken>()).Returns(Failure(provisionError));

        // Act
        var result = await _handler.HandleAsync(new PublishEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), provisionError);
        await _eventRepository.DidNotReceive().TryPublishAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryPublishAsyncLosesTheRace_ReturnsFailureWithEventNotDraftError()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventSeatRepository.ProvisionAsync(evt.Id, evt.VenueId, Arg.Any<CancellationToken>()).Returns(Success());
        _eventRepository.TryPublishAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new PublishEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), PublishEventCommandHandler.Errors.EventNotDraft);
    }
}