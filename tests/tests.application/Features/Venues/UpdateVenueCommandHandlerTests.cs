using EventReservation.Application.Features.Venues;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Venues;

[TestClass]
public class UpdateVenueCommandHandlerTests
{
    private IVenueRepository _venueRepository = null!;
    private UpdateVenueCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = Substitute.For<IVenueRepository>();
        _handler = new UpdateVenueCommandHandler(_venueRepository);
    }

    private Venue CreateValidVenue()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        return venue;
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessAndCallsUpdateAsync()
    {
        // Arrange
        var venue = CreateValidVenue();
        var command = new UpdateVenueCommand(venue.Id, "Riverside Arena", "456 Oak Ave, Shelbyville", 7500);

        _venueRepository.GetByIdAsync(venue.Id, Arg.Any<CancellationToken>()).Returns(Success(venue));
        _venueRepository.UpdateAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Venue>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(command.Name, result.Value.Name);
        Assert.AreEqual(command.Address, result.Value.Address);
        Assert.AreEqual(command.Capacity, result.Value.Capacity);
        await _venueRepository.Received(1).UpdateAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithMultipleInvalidFields_AccumulatesAllErrors_AndNeverCallsUpdateAsync()
    {
        // Arrange - proves Combine's accumulation across
        // Rename/Relocate/ChangeCapacity, not just short-circuit on the first
        var venue = CreateValidVenue();
        var command = new UpdateVenueCommand(venue.Id, "", "", 0);

        _venueRepository.GetByIdAsync(venue.Id, Arg.Any<CancellationToken>()).Returns(Success(venue));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyName);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyAddress);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.InvalidCapacity);
        await _venueRepository.DidNotReceive().UpdateAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenVenueNotFound_PropagatesFailure_AndNeverCallsUpdateAsync()
    {
        // Arrange
        var venueId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _venueRepository.GetByIdAsync(venueId, Arg.Any<CancellationToken>()).Returns(Failure<Venue>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new UpdateVenueCommand(venueId, "Riverside Arena", "456 Oak Ave, Shelbyville", 7500));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
        await _venueRepository.DidNotReceive().UpdateAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>());
    }
}