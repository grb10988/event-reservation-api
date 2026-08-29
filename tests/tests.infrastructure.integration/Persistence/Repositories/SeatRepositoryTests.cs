using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Domain.Results.Extensions;
using EventReservation.Infrastructure.Persistence;
using EventReservation.Infrastructure.Persistence.Repositories;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class SeatRepositoryTests : IntegrationTestBase
{
    private ISeatRepository _seatRepository = null!;
    private IVenueRepository _venueRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        _seatRepository = new SeatRepository(ConnectionFactory);
        _venueRepository = new VenueRepository(ConnectionFactory);
    }

    private async Task<Guid> SeedVenueAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(result.IsSuccess);
        return result.Value.Id;
    }

    private async Task<Guid> SeedAvailableSeatAsync(Guid venueId, string section = "A", int row = 1, int number = 1)
    {
        var seat = Seat.Create(venueId, section, row, number).Value;
        Assert.IsNotNull(seat);
        var result = await _seatRepository.AddAsync(seat);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    // ============================================================
    // AddAsync / GetByIdAsync
    // ============================================================

    [TestMethod]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFieldsCorrectly()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seat = Seat.Create(venueId, "B", 3, 12).Value;
        Assert.IsNotNull(seat);

        // Act
        var addResult = await _seatRepository.AddAsync(seat);
        var getResult = await _seatRepository.GetByIdAsync(seat.Id);

        // Assert
        Assert.IsTrue(addResult.IsSuccess);
        Assert.IsTrue(getResult.IsSuccess);
        Assert.AreEqual(seat.Id, getResult.Value.Id);
        Assert.AreEqual(venueId, getResult.Value.VenueId);
        Assert.AreEqual("B", getResult.Value.Section);
        Assert.AreEqual(3, getResult.Value.Row);
        Assert.AreEqual(12, getResult.Value.Number);
        Assert.AreEqual(SeatStatus.Available, getResult.Value.Status);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenSeatDoesNotExist_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _seatRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    [TestMethod]
    public async Task AddAsync_WithDuplicateSectionRowNumberAtSameVenue_ReturnsDuplicateRecordFailure()
    {
        // Arrange - this is the test unit tests could never write: a real
        // UniqueViolation, thrown by a real Postgres, translated by the
        // real DatabaseExceptionTranslator.SqlState switch
        var venueId = await SeedVenueAsync();
        await SeedAvailableSeatAsync(venueId, "A", 1, 1);
        var duplicateSeat = Seat.Create(venueId, "A", 1, 1).Value;
        Assert.IsNotNull(duplicateSeat);

        // Act
        var result = await _seatRepository.AddAsync(duplicateSeat);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), DatabaseExceptionMapper.Errors.DuplicateRecord);
    }

    // ============================================================
    // GetByVenueIdAsync
    // ============================================================

    [TestMethod]
    public async Task GetByVenueIdAsync_ReturnsOnlySeatsForThatVenue_OrderedBySectionRowNumber()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var otherVenueId = await SeedVenueAsync();
        await SeedAvailableSeatAsync(venueId, "B", 1, 1);
        await SeedAvailableSeatAsync(venueId, "A", 2, 1);
        await SeedAvailableSeatAsync(venueId, "A", 1, 1);
        await SeedAvailableSeatAsync(otherVenueId, "A", 1, 1);

        // Act
        var result = await _seatRepository.GetByVenueIdAsync(venueId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(3, result.Value);
        CollectionAssert.AreEqual(
            new[] { "A-1-1", "A-2-1", "B-1-1" },
            result.Value.Select(s => $"{s.Section}-{s.Row}-{s.Number}").ToList());
    }

    // ============================================================
    // TryHoldAsync
    // ============================================================

    [TestMethod]
    public async Task TryHoldAsync_WhenSeatIsAvailable_ReturnsTrueAndPersistsHeldStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);

        // Act
        var holdResult = await _seatRepository.TryHoldAsync(seatId);
        var getResult = await _seatRepository.GetByIdAsync(seatId);

        // Assert
        Assert.IsTrue(holdResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(SeatStatus.Held, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryHoldAsync_WhenSeatIsAlreadyHeld_ReturnsFalse_AndDoesNotThrow()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);
        await _seatRepository.TryHoldAsync(seatId);

        // Act
        var result = await _seatRepository.TryHoldAsync(seatId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }

    [TestMethod]
    public async Task TryHoldAsync_WhenTwoRequestsRaceForTheSameSeat_ExactlyOneSucceeds()
    {
        // Arrange - the whole reason this project and this test project exist
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);

        // Act - fire two concurrent hold attempts at the same seat
        var results = await Task.WhenAll(
            _seatRepository.TryHoldAsync(seatId),
            _seatRepository.TryHoldAsync(seatId));

        // Assert - exactly one wins, exactly one loses, no exception,
        // no double-hold - proving the WHERE status = 'Available' clause
        // is genuinely atomic under real concurrent load, not just correct
        // in theory
        Assert.IsTrue(results.All(r => r.IsSuccess));
        var successCount = results.Count(r => r.Value);
        Assert.AreEqual(1, successCount);

        var finalState = await _seatRepository.GetByIdAsync(seatId);
        Assert.IsNotNull(finalState.Value);
        Assert.AreEqual(SeatStatus.Held, finalState.Value.Status);
    }

    // ============================================================
    // TryReserveAsync
    // ============================================================

    [TestMethod]
    public async Task TryReserveAsync_WhenSeatIsHeld_ReturnsTrueAndPersistsReservedStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);
        await _seatRepository.TryHoldAsync(seatId);

        // Act
        var reserveResult = await _seatRepository.TryReserveAsync(seatId);
        var getResult = await _seatRepository.GetByIdAsync(seatId);

        // Assert
        Assert.IsTrue(reserveResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(SeatStatus.Reserved, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryReserveAsync_WhenSeatIsAvailable_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);

        // Act
        var result = await _seatRepository.TryReserveAsync(seatId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryReleaseAsync
    // ============================================================

    [TestMethod]
    public async Task TryReleaseAsync_WhenSeatIsHeld_ReturnsTrueAndPersistsAvailableStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);
        await _seatRepository.TryHoldAsync(seatId);

        // Act
        var releaseResult = await _seatRepository.TryReleaseAsync(seatId);
        var getResult = await _seatRepository.GetByIdAsync(seatId);

        // Assert
        Assert.IsTrue(releaseResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(SeatStatus.Available, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryReleaseAsync_WhenSeatIsReserved_ReturnsTrueAndPersistsAvailableStatus()
    {
        // Arrange - proves the "status IN (Held, Reserved)" clause via
        // Dapper's array-expansion, the one piece flagged as worth
        // confirming for real rather than trusting on documentation alone
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);
        await _seatRepository.TryHoldAsync(seatId);
        await _seatRepository.TryReserveAsync(seatId);

        // Act
        var releaseResult = await _seatRepository.TryReleaseAsync(seatId);
        var getResult = await _seatRepository.GetByIdAsync(seatId);

        // Assert
        Assert.IsTrue(releaseResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(SeatStatus.Available, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryReleaseAsync_WhenSeatIsAlreadyAvailable_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var seatId = await SeedAvailableSeatAsync(venueId);

        // Act
        var result = await _seatRepository.TryReleaseAsync(seatId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value);
    }
}