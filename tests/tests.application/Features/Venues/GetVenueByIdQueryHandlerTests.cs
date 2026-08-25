using EventReservation.Application.Features.Venues;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Venues;

[TestClass]
public class GetVenueByIdQueryHandlerTests
{
    private IVenueRepository _venueRepository = null!;
    private GetVenueByIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = Substitute.For<IVenueRepository>();
        _handler = new GetVenueByIdQueryHandler(_venueRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenVenueExists_ReturnsMappedResult()
    {
        // Arrange
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        _venueRepository.GetByIdAsync(venue.Id, Arg.Any<CancellationToken>()).Returns(Success(venue));

        // Act
        var result = await _handler.HandleAsync(new GetVenueByIdQuery(venue.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(venue.Id, result.Value.Id);
        Assert.AreEqual(venue.Name, result.Value.Name);
    }

    [TestMethod]
    public async Task HandleAsync_WhenVenueDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var venueId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _venueRepository.GetByIdAsync(venueId, Arg.Any<CancellationToken>()).Returns(Failure<Venue>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetVenueByIdQuery(venueId));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}