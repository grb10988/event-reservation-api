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
    private FakeTimeProvider _timeProvider = null!;
    private PublishEventCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new PublishEventCommandHandler(_eventRepository);
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
    public async Task HandleAsync_WhenDraft_ReturnsSuccessAndCallsTryPublishAsync()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryPublishAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new PublishEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _eventRepository.Received(1).TryPublishAsync(evt.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenAlreadyPublished_ReturnsFailureWithCannotPublishError_AndNeverCallsTryPublishAsync()
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
        await _eventRepository.DidNotReceive().TryPublishAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryPublishAsyncLosesTheRace_ReturnsFailureWithEventNotDraftError()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryPublishAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new PublishEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), PublishEventCommandHandler.Errors.EventNotDraft);
    }
}