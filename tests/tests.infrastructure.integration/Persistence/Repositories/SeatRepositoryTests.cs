using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class SeatRepositoryTests : IntegrationTestBase
{
    private ISeatRepository _seatRepository = null!;
    private IVenueRepository _venueRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        _seatRepository = new SeatRepository(ConnectionFactory, NullLogger<SeatRepository>.Instance);
        _venueRepository = new VenueRepository(ConnectionFactory, NullLogger<VenueRepository>.Instance);
    }

    private async Task<Guid> SeedVenueAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(result.IsSuccess);
        return result.Value.Id;
    }

    private async Task<Guid> SeedSeatAsync(Guid venueId, string section = "A", int row = 1, int number = 1)
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
    public async Task AddAsync_WithDuplicateSectionRowNumberAtSameVenue_ReturnsDuplicateRecordFailure_AndDoesNotLog()
    {
        // Arrange - this is the test unit tests could never write: a real
        // UniqueViolation, thrown by a real Postgres, translated by the
        // real DatabaseExceptionMapper.SqlState switch. Uses its own
        // repository instance with an observable fake logger, rather than
        // the class's shared _seatRepository (which uses NullLogger and
        // can't be asserted against), since this test's whole point is
        // verifying the logger was never called for this Conflict-category
        // error.
        var logger = Substitute.For<ILogger<SeatRepository>>();
        var seatRepository = new SeatRepository(ConnectionFactory, logger);

        var venueId = await SeedVenueAsync();
        await SeedSeatAsync(venueId, "A", 1, 1);
        var duplicateSeat = Seat.Create(venueId, "A", 1, 1).Value;
        Assert.IsNotNull(duplicateSeat);

        // Act
        var result = await seatRepository.AddAsync(duplicateSeat);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), DatabaseExceptionMapper.Errors.DuplicateRecord);
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(), Arg.Any<EventId>(), Arg.Any<object>(), Arg.Any<Exception>(), Arg.Any<Func<object, Exception?, string>>());
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
        await SeedSeatAsync(venueId, "B", 1, 1);
        await SeedSeatAsync(venueId, "A", 2, 1);
        await SeedSeatAsync(venueId, "A", 1, 1);
        await SeedSeatAsync(otherVenueId, "A", 1, 1);

        // Act
        var result = await _seatRepository.GetByVenueIdAsync(venueId);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.HasCount(3, result.Value);
        CollectionAssert.AreEqual(
            new[] { "A-1-1", "A-2-1", "B-1-1" },
            result.Value.Select(s => $"{s.Section}-{s.Row}-{s.Number}").ToList());
    }
}