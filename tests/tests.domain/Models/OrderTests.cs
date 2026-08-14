using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class OrderTests
{
    private static readonly Guid ValidCustomerId = Guid.NewGuid();
    private static readonly IReadOnlyCollection<Guid> ValidReservationIds = [Guid.NewGuid(), Guid.NewGuid()];
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

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
        var result = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidCustomerId, result.Value.CustomerId);
        CollectionAssert.AreEqual(ValidReservationIds.ToList(), result.Value.ReservationIds.ToList());
        Assert.AreEqual(OrderStatus.Pending, result.Value.Status);
        Assert.AreEqual(Now, result.Value.CreatedAt);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    public void Create_ReturnsOrderWithNoConfirmationNumberYet()
    {
        // Act
        var result = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.Value.ConfirmationNumber);
    }

    [TestMethod]
    public void Create_WithEmptyCustomerId_ReturnsFailureWithEmptyCustomerIdError()
    {
        // Act
        var result = Order.Create(Guid.Empty, ValidReservationIds, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.EmptyCustomerId);
    }

    [TestMethod]
    public void Create_WithNullReservationIds_ReturnsFailureWithEmptyReservationIdsError()
    {
        // Act
        var result = Order.Create(ValidCustomerId, null!, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.EmptyReservationIds);
    }

    [TestMethod]
    public void Create_WithEmptyReservationIdsCollection_ReturnsFailureWithEmptyReservationIdsError()
    {
        // Act
        var result = Order.Create(ValidCustomerId, [], _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.EmptyReservationIds);
    }

    [TestMethod]
    public void Create_WithReservationIdsContainingEmptyGuid_ReturnsFailureWithInvalidReservationIdError()
    {
        // Arrange
        IReadOnlyCollection<Guid> reservationIds = [Guid.NewGuid(), Guid.Empty];

        // Act
        var result = Order.Create(ValidCustomerId, reservationIds, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.InvalidReservationId);
    }

    [TestMethod]
    public void Create_WithDuplicateReservationIds_ReturnsFailureWithDuplicateReservationIdsError()
    {
        // Arrange
        var duplicateId = Guid.NewGuid();
        IReadOnlyCollection<Guid> reservationIds = [duplicateId, duplicateId];

        // Act
        var result = Order.Create(ValidCustomerId, reservationIds, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.DuplicateReservationIds);
    }

    [TestMethod]
    public void Create_WithEmptyGuidAndDuplicateInSameList_AccumulatesBothErrors()
    {
        // Arrange - proves phase two runs its checks together once phase one
        // has already confirmed the collection itself is present and non-empty
        var duplicateId = Guid.NewGuid();
        IReadOnlyCollection<Guid> reservationIds = [duplicateId, duplicateId, Guid.Empty];

        // Act
        var result = Order.Create(ValidCustomerId, reservationIds, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(2, result.Errors.Count);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.InvalidReservationId);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.DuplicateReservationIds);
    }

    [TestMethod]
    public void Create_WithEmptyCustomerIdAndNullReservationIds_OnlyAccumulatesPhaseOneErrors()
    {
        // Act - proves the early return: phase two (list-contents checks)
        // never runs when phase one already failed on a null collection
        var result = Order.Create(Guid.Empty, null!, _timeProvider);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(2, result.Errors.Count);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.EmptyCustomerId);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.EmptyReservationIds);
    }

    // ============================================================
    // Complete
    // ============================================================

    [TestMethod]
    public void Complete_WhenPending_ReturnsSuccessWithCompletedStatus()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);

        // Act
        var result = order.Complete();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Completed, result.Value.Status);
    }

    [TestMethod]
    public void Complete_WhenPending_SetsConfirmationNumberInExpectedFormat()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);

        // Act
        var result = order.Complete();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        var confirmationNumber = result.Value.ConfirmationNumber;
        Assert.IsNotNull(confirmationNumber);

        var segments = confirmationNumber.Split('-');
        Assert.AreEqual(Order.ConfirmationSegmentCount, segments.Length);

        foreach (var segment in segments)
        {
            Assert.AreEqual(Order.ConfirmationSegmentLength, segment.Length);
            Assert.IsTrue(segment.All(c => Order.ConfirmationCharacters.Contains(c)));
        }
    }

    [TestMethod]
    public void Complete_CalledOnTwoDifferentOrders_GeneratesDifferentConfirmationNumbers()
    {
        // Arrange
        var first = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        var second = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);

        // Act
        first.Complete();
        second.Complete();

        // Assert
        Assert.AreNotEqual(first.ConfirmationNumber, second.ConfirmationNumber);
    }

    [TestMethod]
    public void Complete_WhenAlreadyCompleted_ReturnsFailureWithCannotCompleteError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Complete();

        // Act
        var result = order.Complete();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotComplete);
    }

    [TestMethod]
    public void Complete_WhenCancelled_ReturnsFailureWithCannotCompleteError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Cancel();

        // Act
        var result = order.Complete();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotComplete);
    }

    [TestMethod]
    public void Complete_WhenRefunded_ReturnsFailureWithCannotCompleteError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Complete();
        order.Refund();

        // Act
        var result = order.Complete();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotComplete);
    }

    // ============================================================
    // Cancel
    // ============================================================

    [TestMethod]
    public void Cancel_WhenPending_ReturnsSuccessWithCancelledStatus()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);

        // Act
        var result = order.Cancel();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Cancelled, result.Value.Status);
    }

    [TestMethod]
    public void Cancel_WhenCompleted_ReturnsFailureWithCannotCancelError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Complete();

        // Act
        var result = order.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotCancel);
    }

    [TestMethod]
    public void Cancel_WhenAlreadyCancelled_ReturnsFailureWithCannotCancelError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Cancel();

        // Act
        var result = order.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotCancel);
    }

    [TestMethod]
    public void Cancel_WhenRefunded_ReturnsFailureWithCannotCancelError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Complete();
        order.Refund();

        // Act
        var result = order.Cancel();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotCancel);
    }

    // ============================================================
    // Refund
    // ============================================================

    [TestMethod]
    public void Refund_WhenCompleted_ReturnsSuccessWithRefundedStatus()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Complete();

        // Act
        var result = order.Refund();

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(OrderStatus.Refunded, result.Value.Status);
    }

    [TestMethod]
    public void Refund_WhenPending_ReturnsFailureWithCannotRefundError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);

        // Act
        var result = order.Refund();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotRefund);
    }

    [TestMethod]
    public void Refund_WhenCancelled_ReturnsFailureWithCannotRefundError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Cancel();

        // Act
        var result = order.Refund();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotRefund);
    }

    [TestMethod]
    public void Refund_WhenAlreadyRefunded_ReturnsFailureWithCannotRefundError()
    {
        // Arrange
        var order = Order.Create(ValidCustomerId, ValidReservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        order.Complete();
        order.Refund();

        // Act
        var result = order.Refund();

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotRefund);
    }
}