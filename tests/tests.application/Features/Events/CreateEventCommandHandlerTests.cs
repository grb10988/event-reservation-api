using EventReservation.Application.Features.Events;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class CreateEventCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidStartTime = Now.AddDays(30);
    private static readonly DateTimeOffset ValidEndTime = ValidStartTime.AddHours(3);

    private IEventRepository _eventRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CreateEventCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CreateEventCommandHandler(_eventRepository, _timeProvider);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessAndCallsAddAsync()
    {
        // Arrange
        var command = new CreateEventCommand(VenueId, "Summer Concert", "An outdoor concert.", ValidStartTime, ValidEndTime, 50.00m, EventStatus.Draft);
        _eventRepository.AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Event>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(command.Name, result.Value.Name);
        Assert.AreEqual(EventStatus.Draft, result.Value.Status);
        await _eventRepository.Received(1).AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithInvalidName_ReturnsFailureWithEmptyNameError_AndNeverCallsAddAsync()
    {
        // Arrange
        var command = new CreateEventCommand(VenueId, "", "An outdoor concert.", ValidStartTime, ValidEndTime, 50.00m, EventStatus.Draft);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyName);
        await _eventRepository.DidNotReceive().AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithStartTimeInThePast_ReturnsFailureWithInvalidStartTimeError()
    {
        // Arrange - proves the handler's injected FakeTimeProvider is what
        // Event.Create actually validates against, not real wall-clock time
        var command = new CreateEventCommand(VenueId, "Summer Concert", "An outdoor concert.", Now.AddDays(-1), ValidEndTime, 50.00m, EventStatus.Draft);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
    }

    [TestMethod]
    public async Task HandleAsync_WhenAddAsyncFails_PropagatesFailure()
    {
        // Arrange
        var command = new CreateEventCommand(VenueId, "Summer Concert", "An outdoor concert.", ValidStartTime, ValidEndTime, 50.00m, EventStatus.Draft);
        var dbError = new ResultError("TEST", "db error");
        _eventRepository.AddAsync(Arg.Any<Event>(), Arg.Any<CancellationToken>()).Returns(Failure<Event>(dbError));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), dbError);
    }
}