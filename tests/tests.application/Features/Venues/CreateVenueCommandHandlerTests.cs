using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Venues;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Venues;

[TestClass]
public class CreateVenueCommandHandlerTests
{
    private IVenueRepository _venueRepository = null!;
    private IDomainEventPipelineBehavior _eventPublisher = null!;
    private TimeProvider _timeProvider = null!;
    private CreateVenueCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = Substitute.For<IVenueRepository>();
        _eventPublisher = Substitute.For<IDomainEventPipelineBehavior>();
        _timeProvider = Substitute.For<TimeProvider>();

        _eventPublisher
            .PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Success()));

        _handler = new CreateVenueCommandHandler(
            _venueRepository,
            _eventPublisher,
            _timeProvider);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessAndCallsAddAsync()
    {
        // Arrange
        var command = new CreateVenueCommand("City Amphitheater", "123 Main St, Springfield", 5000);
        _venueRepository.AddAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Venue>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(command.Name, result.Value.Name);
        Assert.AreEqual(command.Capacity, result.Value.Capacity);
        await _venueRepository.Received(1).AddAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithInvalidName_ReturnsFailureWithEmptyNameError_AndNeverCallsAddAsync()
    {
        // Arrange
        var command = new CreateVenueCommand("", "123 Main St, Springfield", 5000);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyName);
        await _venueRepository.DidNotReceive().AddAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithInvalidCapacity_ReturnsFailureWithInvalidCapacityError_AndNeverCallsAddAsync()
    {
        // Arrange
        var command = new CreateVenueCommand("City Amphitheater", "123 Main St, Springfield", 0);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.InvalidCapacity);
        await _venueRepository.DidNotReceive().AddAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenAddAsyncFails_PropagatesFailure()
    {
        // Arrange
        var command = new CreateVenueCommand("City Amphitheater", "123 Main St, Springfield", 5000);
        var dbError = new ResultError("TEST", "db error");
        _venueRepository.AddAsync(Arg.Any<Venue>(), Arg.Any<CancellationToken>()).Returns(Failure<Venue>(dbError));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), dbError);
    }
}