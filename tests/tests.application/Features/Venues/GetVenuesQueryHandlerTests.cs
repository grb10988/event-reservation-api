using EventReservation.Application.Features.Venues;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Venues;

[TestClass]
public class GetVenuesQueryHandlerTests
{
    private IVenueRepository _venueRepository = null!;
    private GetVenuesQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = Substitute.For<IVenueRepository>();
        _handler = new GetVenuesQueryHandler(_venueRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenVenuesExist_ReturnsMappedSummaries()
    {
        // Arrange
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        _venueRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Venue>>([venue]));

        // Act
        var result = await _handler.HandleAsync(new GetVenuesQuery());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Value.Count);
        Assert.AreEqual(venue.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNoVenuesExist_ReturnsEmptyList()
    {
        // Arrange
        _venueRepository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Venue>>([]));

        // Act
        var result = await _handler.HandleAsync(new GetVenuesQuery());

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Value.Count);
    }
}