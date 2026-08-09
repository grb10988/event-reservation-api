using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class EventTests
{
    private static readonly Guid ValidVenueId = Guid.NewGuid();
    private const string ValidName = "Summer Concert";
    private const string ValidDescription = "An outdoor summer concert series.";
    private const decimal ValidTicketPrice = 50.00m;

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
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidName, result.Value.Name);
        Assert.AreEqual(ValidVenueId, result.Value.VenueId);
        Assert.AreEqual(ValidDescription, result.Value.Description);
        Assert.AreEqual(ValidStartTime, result.Value.StartTime);
        Assert.AreEqual(ValidEndTime, result.Value.EndTime);
        Assert.AreEqual(ValidTicketPrice, result.Value.TicketPrice);
        Assert.AreEqual(EventStatus.Draft, result.Value.Status);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidName_ReturnsFailureWithEmptyNameError(string? name)
    {
        // Act
        var result = Event.Create(ValidVenueId, name!, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyName);
    }

    [TestMethod]
    public void Create_WithInvalidVenueId_ReturnsFailureWithEmptyVenueIdError()
    {
        // Act
        var result = Event.Create(Guid.Empty, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyVenueId);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidDescription_ReturnsFailureWithEmptyDescriptionError(string? description)
    {
        // Act
        var result = Event.Create(ValidVenueId, ValidName, description!, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider);

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
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, pastStartTime, ValidEndTime, ValidTicketPrice, _timeProvider);

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
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, endBeforeStart, ValidTicketPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-100)]
    public void Create_WithNegativeTicketPrice_ReturnsFailureWithInvalidTicketPriceError(int ticketPrice)
    {
        // Act
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ticketPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidTicketPrice);
    }

    [TestMethod]
    public void Create_WithZeroTicketPrice_ReturnsSuccess()
    {
        // Act
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, 0m, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0m, result.Value.TicketPrice);
    }

    [TestMethod]
    public void Create_WithAllInvalidInputs_AccumulatesAllErrors()
    {
        // Act
        var result = Event.Create(Guid.Empty, "", "", Now.AddDays(-1), Now.AddDays(-2), -1m, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(6, result.Errors.Count);
    }

    [TestMethod]
    public void Create_WhenStartTimeEqualsNow_ReturnsFailureWithInvalidStartTimeError()
    {
        // Act
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, Now, ValidEndTime, ValidTicketPrice, _timeProvider);

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
        var draftEvent = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
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
        var publishedEvent = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
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
        var cancelledEvent = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
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
        var draftEvent = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
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
        var publishedEvent = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
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
        var cancelledEvent = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(cancelledEvent);
        cancelledEvent.Cancel();

        // Act
        var result = cancelledEvent.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotCancel);
    }
}