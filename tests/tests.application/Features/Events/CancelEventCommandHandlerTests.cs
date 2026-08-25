using EventReservation.Application.Features.Events;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class CancelEventCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IEventRepository _eventRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CancelEventCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CancelEventCommandHandler(_eventRepository);
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
    public async Task HandleAsync_WhenDraft_ReturnsSuccessAndCallsTryCancelAsync()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task HandleAsync_WhenPublished_ReturnsSuccessAndCallsTryCancelAsync()
    {
        // Arrange - Cancel is valid from either Draft or Published
        var evt = CreateDraftEvent();
        evt.Publish();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task HandleAsync_WhenAlreadyCancelled_ReturnsFailureWithCannotCancelError_AndNeverCallsTryCancelAsync()
    {
        // Arrange
        var evt = CreateDraftEvent();
        evt.Cancel();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotCancel);
        await _eventRepository.DidNotReceive().TryCancelAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryCancelAsyncLosesTheRace_ReturnsFailureWithEventAlreadyCancelledError()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CancelEventCommandHandler.Errors.EventAlreadyCancelled);
    }
}