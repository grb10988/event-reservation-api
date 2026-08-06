using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class EventTests
{
    private const string ValidName = "Summer Concert";
    private const string ValidVenue = "City Amphitheater";
    private const string ValidDescription = "An outdoor summer concert series.";
    private const int ValidCapacity = 500;

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidStartTime = Now.AddDays(30);
    private static readonly DateTimeOffset ValidEndTime = ValidStartTime.AddHours(3);

    private FakeTimeProvider _timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        _timeProvider = new FakeTimeProvider(Now);
    }

    // ============================================================
    // Create
    // ============================================================

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithExpectedValues()
    {
        // Act
        var result = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidName, result.Value.Name);
        Assert.AreEqual(ValidVenue, result.Value.Venue);
        Assert.AreEqual(ValidDescription, result.Value.Description);
        Assert.AreEqual(ValidStartTime, result.Value.StartTime);
        Assert.AreEqual(ValidEndTime, result.Value.EndTime);
        Assert.AreEqual(ValidCapacity, result.Value.Capacity);
        Assert.AreEqual(EventStatus.Draft, result.Value.Status);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidName_ReturnsFailureWithEmptyNameError(string? name)
    {
        // Act
        var result = Event.Create(name!, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyName);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidVenue_ReturnsFailureWithEmptyVenueError(string? venue)
    {
        // Act
        var result = Event.Create(ValidName, venue!, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyVenue);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidDescription_ReturnsFailureWithEmptyDescriptionError(string? description)
    {
        // Act
        var result = Event.Create(ValidName, ValidVenue, description!, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyDescription);
    }

    [TestMethod]
    public void Create_WithStartTimeInThePast_ReturnsFailureWithInvalidStartTimeError()
    {
        // Arrange
        var pastStartTime = Now.AddDays(-1);

        // Act
        var result = Event.Create(ValidName, ValidVenue, ValidDescription, pastStartTime, ValidEndTime, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
    }

    [TestMethod]
    public void Create_WithEndTimeBeforeStartTime_ReturnsFailureWithInvalidEndTimeError()
    {
        // Arrange
        var endBeforeStart = ValidStartTime.AddHours(-1);

        // Act
        var result = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, endBeforeStart, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_WithInvalidCapacity_ReturnsFailureWithInvalidCapacityError(int capacity)
    {
        // Act
        var result = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, capacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidCapacity);
    }

    [TestMethod]
    public void Create_WithAllInvalidInputs_AccumulatesAllErrors()
    {
        // Act
        var result = Event.Create("", "", "", Now.AddDays(-1), Now.AddDays(-2), 0, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(6, result.Errors.Count);
    }

    [TestMethod]
    public void Create_WhenStartTimeEqualsNow_ReturnsFailureWithInvalidStartTimeError()
    {
        // Act - exact boundary: "now" is not "in the future"
        var result = Event.Create(ValidName, ValidVenue, ValidDescription, Now, ValidEndTime, ValidCapacity, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
    }

    // ============================================================
    // Publish
    // ============================================================

    [TestMethod]
    public void Publish_WhenDraft_ReturnsSuccessWithPublishedStatus()
    {
        // Arrange
        var draftEvent = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider).Value;
        Assert.IsNotNull(draftEvent);

        // Act
        var result = draftEvent.Publish();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventStatus.Published, result.Value.Status);
    }

    [TestMethod]
    public void Publish_WhenAlreadyPublished_ReturnsFailureWithCannotPublishError()
    {
        // Arrange
        var publishedEvent = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider).Value;
        Assert.IsNotNull(publishedEvent);
        publishedEvent.Publish();

        // Act
        var result = publishedEvent.Publish();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotPublish);
    }

    [TestMethod]
    public void Publish_WhenCancelled_ReturnsFailureWithCannotPublishError()
    {
        // Arrange
        var cancelledEvent = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider).Value;
        Assert.IsNotNull(cancelledEvent);
        cancelledEvent.Cancel();

        // Act
        var result = cancelledEvent.Publish();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotPublish);
    }

    // ============================================================
    // Cancel
    // ============================================================

    [TestMethod]
    public void Cancel_WhenDraft_ReturnsSuccessWithCancelledStatus()
    {
        // Arrange
        var draftEvent = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider).Value;
        Assert.IsNotNull(draftEvent);

        // Act
        var result = draftEvent.Cancel();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventStatus.Cancelled, result.Value.Status);
    }

    [TestMethod]
    public void Cancel_WhenPublished_ReturnsSuccessWithCancelledStatus()
    {
        // Arrange
        var publishedEvent = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider).Value;
        Assert.IsNotNull(publishedEvent);
        publishedEvent.Publish();

        // Act
        var result = publishedEvent.Cancel();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(EventStatus.Cancelled, result.Value.Status);
    }

    [TestMethod]
    public void Cancel_WhenAlreadyCancelled_ReturnsFailureWithCannotCancelError()
    {
        // Arrange
        var cancelledEvent = Event.Create(ValidName, ValidVenue, ValidDescription, ValidStartTime, ValidEndTime, ValidCapacity, _timeProvider).Value;
        Assert.IsNotNull(cancelledEvent);
        cancelledEvent.Cancel();

        // Act
        var result = cancelledEvent.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotCancel);
    }
}