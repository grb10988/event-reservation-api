using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class EventRepositoryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IVenueRepository _venueRepository = null!;
    private IEventRepository _eventRepository = null!;
    private FakeTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = new VenueRepository(ConnectionFactory, NullLogger<VenueRepository>.Instance);
        _eventRepository = new EventRepository(ConnectionFactory, NullLogger<EventRepository>.Instance);
        _timeProvider = new FakeTimeProvider(Now);
    }

    private async Task<Guid> SeedVenueAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Event> SeedDraftEventAsync(Guid venueId, string name = "Summer Concert", decimal ticketPrice = 50.00m)
    {
        var evt = Event.Create(venueId, name, "An outdoor concert.", Now.AddDays(30), Now.AddDays(30).AddHours(3), ticketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var result = await _eventRepository.AddAsync(evt);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value;
    }

    // ============================================================
    // AddAsync / GetByIdAsync
    // ============================================================

    [TestMethod]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFieldsCorrectly()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var evt = Event.Create(venueId, "Winter Gala", "A rescheduled gala.", Now.AddDays(45), Now.AddDays(45).AddHours(2), 75.00m, _timeProvider).Value;
        Assert.IsNotNull(evt);

        // Act
        var addResult = await _eventRepository.AddAsync(evt);
        var getResult = await _eventRepository.GetByIdAsync(evt.Id);

        // Assert
        Assert.IsTrue(addResult.IsSuccess, string.Join("; ", addResult.ToFailureMessages()));
        Assert.IsTrue(getResult.IsSuccess, string.Join("; ", getResult.ToFailureMessages()));
        Assert.AreEqual(evt.Id, getResult.Value.Id);
        Assert.AreEqual(venueId, getResult.Value.VenueId);
        Assert.AreEqual("Winter Gala", getResult.Value.Name);
        Assert.AreEqual("A rescheduled gala.", getResult.Value.Description);
        Assert.AreEqual(evt.StartTime, getResult.Value.StartTime);
        Assert.AreEqual(evt.EndTime, getResult.Value.EndTime);
        Assert.AreEqual(75.00m, getResult.Value.TicketPrice);
        Assert.AreEqual(EventStatus.Draft, getResult.Value.Status);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenEventDoesNotExist_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _eventRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // GetByVenueIdAsync
    // ============================================================

    [TestMethod]
    public async Task GetByVenueIdAsync_ReturnsOnlyEventsForThatVenue_OrderedByStartTime()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var otherVenueId = await SeedVenueAsync();

        var laterEvent = Event.Create(venueId, "Later Show", "desc", Now.AddDays(60), Now.AddDays(60).AddHours(2), 40m, _timeProvider).Value;
        Assert.IsNotNull(laterEvent);
        await _eventRepository.AddAsync(laterEvent);

        var earlierEvent = Event.Create(venueId, "Earlier Show", "desc", Now.AddDays(10), Now.AddDays(10).AddHours(2), 40m, _timeProvider).Value;
        Assert.IsNotNull(earlierEvent);
        await _eventRepository.AddAsync(earlierEvent);

        await SeedDraftEventAsync(otherVenueId);

        // Act
        var result = await _eventRepository.GetByVenueIdAsync(venueId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.HasCount(2, result.Value);
        Assert.AreEqual(earlierEvent.Id, result.Value[0].Id);
        Assert.AreEqual(laterEvent.Id, result.Value[1].Id);
    }

    [TestMethod]
    public async Task GetByVenueIdAsync_WhenNoEventsExist_ReturnsEmptyList()
    {
        // Arrange
        var venueId = await SeedVenueAsync();

        // Act
        var result = await _eventRepository.GetByVenueIdAsync(venueId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsEmpty(result.Value);
    }

    // ============================================================
    // UpdateAsync
    // ============================================================

    [TestMethod]
    public async Task UpdateAsync_WithValidEvent_PersistsChangedFields_ButNotStatus()
    {
        // Arrange - confirms Status was deliberately excluded from
        // UpdateAsync's SET clause, since only TryPublishAsync/
        // TryCancelAsync are allowed to change it
        var venueId = await SeedVenueAsync();
        var evt = await SeedDraftEventAsync(venueId);

        var renamed = evt.ChangeName("Renamed Concert").Value;
        Assert.IsNotNull(renamed);
        var repriced = renamed.ChangeTicketPrice(99.00m).Value;
        Assert.IsNotNull(repriced);

        // Act
        var updateResult = await _eventRepository.UpdateAsync(repriced);
        var getResult = await _eventRepository.GetByIdAsync(evt.Id);

        // Assert
        Assert.IsTrue(updateResult.IsSuccess, string.Join("; ", updateResult.ToFailureMessages()));
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual("Renamed Concert", getResult.Value.Name);
        Assert.AreEqual(99.00m, getResult.Value.TicketPrice);
        Assert.AreEqual(EventStatus.Draft, getResult.Value.Status);
    }

    [TestMethod]
    public async Task UpdateAsync_WhenEventDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var evt = Event.Create(venueId, "Ghost Event", "desc", Now.AddDays(10), Now.AddDays(10).AddHours(2), 40m, _timeProvider).Value;
        Assert.IsNotNull(evt);

        // Act
        var result = await _eventRepository.UpdateAsync(evt);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // TryPublishAsync
    // ============================================================

    [TestMethod]
    public async Task TryPublishAsync_WhenDraft_ReturnsTrueAndPersistsPublishedStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var evt = await SeedDraftEventAsync(venueId);

        // Act
        var publishResult = await _eventRepository.TryPublishAsync(evt.Id);
        var getResult = await _eventRepository.GetByIdAsync(evt.Id);

        // Assert
        Assert.IsTrue(publishResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventStatus.Published, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryPublishAsync_WhenAlreadyPublished_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var evt = await SeedDraftEventAsync(venueId);
        await _eventRepository.TryPublishAsync(evt.Id);

        // Act
        var result = await _eventRepository.TryPublishAsync(evt.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryCancelAsync
    // ============================================================

    [TestMethod]
    public async Task TryCancelAsync_WhenDraft_ReturnsTrueAndPersistsCancelledStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var evt = await SeedDraftEventAsync(venueId);

        // Act
        var cancelResult = await _eventRepository.TryCancelAsync(evt.Id);
        var getResult = await _eventRepository.GetByIdAsync(evt.Id);

        // Assert
        Assert.IsTrue(cancelResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventStatus.Cancelled, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryCancelAsync_WhenPublished_ReturnsTrueAndPersistsCancelledStatus()
    {
        // Arrange - Cancel is valid from either Draft or Published,
        // proving the "status IN (Draft, Published)" clause
        var venueId = await SeedVenueAsync();
        var evt = await SeedDraftEventAsync(venueId);
        await _eventRepository.TryPublishAsync(evt.Id);

        // Act
        var cancelResult = await _eventRepository.TryCancelAsync(evt.Id);
        var getResult = await _eventRepository.GetByIdAsync(evt.Id);

        // Assert
        Assert.IsTrue(cancelResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(EventStatus.Cancelled, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryCancelAsync_WhenAlreadyCancelled_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var evt = await SeedDraftEventAsync(venueId);
        await _eventRepository.TryCancelAsync(evt.Id);

        // Act
        var result = await _eventRepository.TryCancelAsync(evt.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }
}