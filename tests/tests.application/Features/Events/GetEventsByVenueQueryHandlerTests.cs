using EventReservation.Application.Features.Events;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class GetEventsByVenueQueryHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();
    private IEventRepository _eventRepository = null!;
    private GetEventsByVenueQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _handler = new GetEventsByVenueQueryHandler(_eventRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenEventsExist_ReturnsMappedSummaries()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var evt = Event.Create(VenueId, "Summer Concert", "An outdoor concert.", timeProvider.GetUtcNow().AddDays(30), timeProvider.GetUtcNow().AddDays(30).AddHours(3), 50.00m, timeProvider).Value;
        Assert.IsNotNull(evt);
        _eventRepository.GetByVenueIdAsync(VenueId, Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Event>>([evt]));

        // Act
        var result = await _handler.HandleAsync(new GetEventsByVenueQuery(VenueId));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Value.Count);
        Assert.AreEqual(evt.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNoEventsExist_ReturnsEmptyList()
    {
        // Arrange
        _eventRepository.GetByVenueIdAsync(VenueId, Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Event>>([]));

        // Act
        var result = await _handler.HandleAsync(new GetEventsByVenueQuery(VenueId));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Value.Count);
    }
}