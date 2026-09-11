using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class VenueRepositoryTests : IntegrationTestBase
{
    private IVenueRepository _venueRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = new VenueRepository(ConnectionFactory, NullLogger<VenueRepository>.Instance);
    }

    private async Task<Venue> SeedVenueAsync(string name = "City Amphitheater", string address = "123 Main St, Springfield", int capacity = 5000)
    {
        var venue = Venue.Create(name, address, capacity).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
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
        var venue = Venue.Create("Riverside Arena", "456 Oak Ave, Shelbyville", 7500).Value;
        Assert.IsNotNull(venue);

        // Act
        var addResult = await _venueRepository.AddAsync(venue);
        var getResult = await _venueRepository.GetByIdAsync(venue.Id);

        // Assert
        Assert.IsTrue(addResult.IsSuccess, string.Join("; ", addResult.ToFailureMessages()));
        Assert.IsTrue(getResult.IsSuccess, string.Join("; ", getResult.ToFailureMessages()));
        Assert.AreEqual(venue.Id, getResult.Value.Id);
        Assert.AreEqual("Riverside Arena", getResult.Value.Name);
        Assert.AreEqual("456 Oak Ave, Shelbyville", getResult.Value.Address);
        Assert.AreEqual(7500, getResult.Value.Capacity);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenVenueDoesNotExist_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _venueRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // GetAllAsync
    // ============================================================

    [TestMethod]
    public async Task GetAllAsync_ReturnsEveryVenue()
    {
        // Arrange
        var venue1 = await SeedVenueAsync("City Amphitheater", "123 Main St, Springfield", 5000);
        var venue2 = await SeedVenueAsync("Riverside Arena", "456 Oak Ave, Shelbyville", 7500);

        // Act
        var result = await _venueRepository.GetAllAsync();

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.HasCount(2, result.Value);
        CollectionAssert.Contains(result.Value.Select(v => v.Id).ToList(), venue1.Id);
        CollectionAssert.Contains(result.Value.Select(v => v.Id).ToList(), venue2.Id);
    }

    [TestMethod]
    public async Task GetAllAsync_WhenNoVenuesExist_ReturnsEmptyList()
    {
        // Act
        var result = await _venueRepository.GetAllAsync();

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsEmpty(result.Value);
    }

    // ============================================================
    // UpdateAsync
    // ============================================================

    [TestMethod]
    public async Task UpdateAsync_WithValidVenue_PersistsChangedFields()
    {
        // Arrange
        var venue = await SeedVenueAsync();
        var renamed = venue.ChangeName("Renamed Amphitheater").Value;
        Assert.IsNotNull(renamed);
        var relocated = renamed.ChangeAddress("999 New Address Ave").Value;
        Assert.IsNotNull(relocated);
        var updated = relocated.ChangeCapacity(6000).Value;
        Assert.IsNotNull(updated);

        // Act
        var updateResult = await _venueRepository.UpdateAsync(updated);
        var getResult = await _venueRepository.GetByIdAsync(venue.Id);

        // Assert
        Assert.IsTrue(updateResult.IsSuccess, string.Join("; ", updateResult.ToFailureMessages()));
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual("Renamed Amphitheater", getResult.Value.Name);
        Assert.AreEqual("999 New Address Ave", getResult.Value.Address);
        Assert.AreEqual(6000, getResult.Value.Capacity);
    }

    [TestMethod]
    public async Task UpdateAsync_WhenVenueDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange - a valid Venue that was never persisted
        var venue = Venue.Create("Ghost Venue", "Nowhere", 100).Value;
        Assert.IsNotNull(venue);

        // Act
        var result = await _venueRepository.UpdateAsync(venue);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }
}