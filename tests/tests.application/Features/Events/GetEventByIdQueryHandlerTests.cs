using EventReservation.Application.Features.Events;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class GetEventByIdQueryHandlerTests
{
    private IEventRepository _eventRepository = null!;
    private GetEventByIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _handler = new GetEventByIdQueryHandler(_eventRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenEventExists_ReturnsMappedResult()
    {
        // Arrange
        var timeProvider = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var evt = Event.Create(Guid.NewGuid(), "Summer Concert", "An outdoor concert.", timeProvider.GetUtcNow().AddDays(30), timeProvider.GetUtcNow().AddDays(30).AddHours(3), 50.00m, timeProvider).Value;
        Assert.IsNotNull(evt);
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));

        // Act
        var result = await _handler.HandleAsync(new GetEventByIdQuery(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(evt.Id, result.Value.Id);
        Assert.AreEqual(evt.Name, result.Value.Name);
    }

    [TestMethod]
    public async Task HandleAsync_WhenEventDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _eventRepository.GetByIdAsync(eventId, Arg.Any<CancellationToken>()).Returns(Failure<Event>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetEventByIdQuery(eventId));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}