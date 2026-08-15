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
        Assert.AreEqual(ValidVenueId, result.Value.VenueId);
        Assert.AreEqual(ValidName, result.Value.Name);
        Assert.AreEqual(ValidDescription, result.Value.Description);
        Assert.AreEqual(ValidStartTime, result.Value.StartTime);
        Assert.AreEqual(ValidEndTime, result.Value.EndTime);
        Assert.AreEqual(ValidTicketPrice, result.Value.TicketPrice);
        Assert.AreEqual(EventStatus.Draft, result.Value.Status);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
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
    public void Create_WithInvalidName_ReturnsFailureWithEmptyNameError(string? name)
    {
        // Act
        var result = Event.Create(ValidVenueId, name!, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyName);
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
    public void Create_WhenStartTimeEqualsNow_ReturnsFailureWithInvalidStartTimeError()
    {
        // Act
        var result = Event.Create(ValidVenueId, ValidName, ValidDescription, Now, ValidEndTime, ValidTicketPrice, _timeProvider);

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

    // ============================================================
    // ChangeStartTime
    // ============================================================

    [TestMethod]
    public void ChangeStartTime_WithValidStartTime_ReturnsSuccessWithUpdatedStartTime()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = ValidStartTime.AddHours(1);

        // Act
        var result = evt.ChangeStartTime(newStartTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newStartTime, result.Value.StartTime);
    }

    [TestMethod]
    public void ChangeStartTime_WithStartTimeInThePast_ReturnsFailureWithInvalidStartTimeError()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);

        // Act
        var result = evt.ChangeStartTime(Now.AddDays(-1), _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
    }

    [TestMethod]
    public void ChangeStartTime_WithStartTimeAfterCurrentEndTime_ReturnsFailureWithInvalidEndTimeError()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = ValidEndTime.AddHours(1);

        // Act
        var result = evt.ChangeStartTime(newStartTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
    }

    // ============================================================
    // ChangeEndTime
    // ============================================================

    [TestMethod]
    public void ChangeEndTime_WithValidEndTime_ReturnsSuccessWithUpdatedEndTime()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newEndTime = ValidEndTime.AddHours(2);

        // Act
        var result = evt.ChangeEndTime(newEndTime);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newEndTime, result.Value.EndTime);
    }

    [TestMethod]
    public void ChangeEndTime_WithEndTimeBeforeCurrentStartTime_ReturnsFailureWithInvalidEndTimeError()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newEndTime = ValidStartTime.AddHours(-1);

        // Act
        var result = evt.ChangeEndTime(newEndTime);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
    }

    // ============================================================
    // ChangeTicketPrice
    // ============================================================

    [TestMethod]
    public void ChangeTicketPrice_WithValidPrice_ReturnsSuccessWithUpdatedPrice()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        const decimal newPrice = 75.00m;

        // Act
        var result = evt.ChangeTicketPrice(newPrice);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newPrice, result.Value.TicketPrice);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(-100)]
    public void ChangeTicketPrice_WithNegativePrice_ReturnsFailureWithInvalidTicketPriceError(int newPrice)
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);

        // Act
        var result = evt.ChangeTicketPrice(newPrice);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidTicketPrice);
    }

    // ============================================================
    // ChangeName
    // ============================================================

    [TestMethod]
    public void ChangeName_WithValidName_ReturnsSuccessWithUpdatedName()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        const string newName = "Winter Gala";

        // Act
        var result = evt.ChangeName(newName);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newName, result.Value.Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeName_WithInvalidName_ReturnsFailureWithEmptyNameError(string? newName)
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);

        // Act
        var result = evt.ChangeName(newName!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyName);
    }

    // ============================================================
    // ChangeDescription
    // ============================================================

    [TestMethod]
    public void ChangeDescription_WithValidDescription_ReturnsSuccessWithUpdatedDescription()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        const string newDescription = "A rescheduled winter gala.";

        // Act
        var result = evt.ChangeDescription(newDescription);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newDescription, result.Value.Description);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeDescription_WithInvalidDescription_ReturnsFailureWithEmptyDescriptionError(string? newDescription)
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);

        // Act
        var result = evt.ChangeDescription(newDescription!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.EmptyDescription);
    }

    // ============================================================
    // Reschedule
    // ============================================================

    [TestMethod]
    public void Reschedule_ToLaterTimes_ReturnsSuccessWithUpdatedTimes()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = ValidStartTime.AddDays(10);
        var newEndTime = newStartTime.AddHours(3);

        // Act
        var result = evt.Reschedule(newStartTime, newEndTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newStartTime, result.Value.StartTime);
        Assert.AreEqual(newEndTime, result.Value.EndTime);
    }

    [TestMethod]
    public void Reschedule_ToEarlierTimesBeforeCurrentStart_ReturnsSuccessWithUpdatedTimes()
    {
        // Arrange - proves Reschedule validates the new pair against itself,
        // not against the object's current StartTime/EndTime
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = Now.AddDays(5);
        var newEndTime = newStartTime.AddHours(2);

        // Act
        var result = evt.Reschedule(newStartTime, newEndTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newStartTime, result.Value.StartTime);
        Assert.AreEqual(newEndTime, result.Value.EndTime);
    }

    [TestMethod]
    public void Reschedule_WithStartTimeInThePast_ReturnsFailureWithInvalidStartTimeError()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = Now.AddDays(-1);

        // Act
        var result = evt.Reschedule(newStartTime, ValidEndTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
    }

    [TestMethod]
    public void Reschedule_WithEndTimeBeforeNewStartTime_ReturnsFailureWithInvalidEndTimeError()
    {
        // Arrange
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = ValidStartTime.AddDays(1);
        var newEndTime = newStartTime.AddHours(-1);

        // Act
        var result = evt.Reschedule(newStartTime, newEndTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
    }

    [TestMethod]
    public void Reschedule_WithStartTimeInPastAndEndTimeBeforeStart_AccumulatesBothErrors()
    {
        // Arrange - proves ValidateReschedule does not short-circuit between
        // its two checks, unlike a single field's own two-phase gate
        var evt = Event.Create(ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var newStartTime = Now.AddDays(-1);
        var newEndTime = newStartTime.AddHours(-1);

        // Act
        var result = evt.Reschedule(newStartTime, newEndTime, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(2, result.Errors.Count);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidStartTime);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.InvalidEndTime);
    }

    // ============================================================
    // Rehydrate
    // ============================================================

    [TestMethod]
    public void Rehydrate_WithGivenValues_ReturnsEventWithThoseExactValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var evt = Event.Rehydrate(id, ValidVenueId, ValidName, ValidDescription, ValidStartTime, ValidEndTime, ValidTicketPrice, EventStatus.Published);

        // Assert
        Assert.AreEqual(id, evt.Id);
        Assert.AreEqual(ValidVenueId, evt.VenueId);
        Assert.AreEqual(ValidName, evt.Name);
        Assert.AreEqual(ValidDescription, evt.Description);
        Assert.AreEqual(ValidStartTime, evt.StartTime);
        Assert.AreEqual(ValidEndTime, evt.EndTime);
        Assert.AreEqual(ValidTicketPrice, evt.TicketPrice);
        Assert.AreEqual(EventStatus.Published, evt.Status);
    }
}