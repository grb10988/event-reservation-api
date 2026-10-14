using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class OrderRepositoryTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = TruncateToSeconds(DateTimeOffset.UtcNow);

    private IVenueRepository _venueRepository = null!;
    private ISeatRepository _seatRepository = null!;
    private IEventRepository _eventRepository = null!;
    private IEventSeatRepository _eventSeatRepository = null!;
    private ICustomerRepository _customerRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private IOrderRepository _orderRepository = null!;
    private FakeTimeProvider _timeProvider = null!;

    private async Task ConfirmReservationAsync(Guid reservationId)
    {
        var result = await _reservationRepository.TryConfirmAsync(reservationId);
        Assert.IsTrue(result is { IsSuccess: true, Value: true }, string.Join("; ", result.ToFailureMessages()));
    }

    [TestInitialize]
    public void Setup()
    {
        _venueRepository = new VenueRepository(ConnectionFactory, NullLogger<VenueRepository>.Instance);
        _seatRepository = new SeatRepository(ConnectionFactory, NullLogger<SeatRepository>.Instance);
        _eventRepository = new EventRepository(ConnectionFactory, NullLogger<EventRepository>.Instance);
        _eventSeatRepository = new EventSeatRepository(ConnectionFactory, NullLogger<EventSeatRepository>.Instance);
        _customerRepository = new CustomerRepository(ConnectionFactory, NullLogger<CustomerRepository>.Instance);
        _reservationRepository = new ReservationRepository(ConnectionFactory, NullLogger<ReservationRepository>.Instance);
        _orderRepository = new OrderRepository(ConnectionFactory, NullLogger<OrderRepository>.Instance);
        _timeProvider = new FakeTimeProvider(Now);
    }

    private async Task<Guid> SeedVenueAsync()
    {
        var venue = Venue.Create("City Amphitheater", "123 Main St, Springfield", 5000).Value;
        Assert.IsNotNull(venue);
        var result = await _venueRepository.AddAsync(venue);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedSeatAsync(Guid venueId, string section, int row, int number)
    {
        var seat = Seat.Create(venueId, section, row, number).Value;
        Assert.IsNotNull(seat);
        var result = await _seatRepository.AddAsync(seat);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedEventAsync(Guid venueId)
    {
        var evt = Event.Create(venueId, "Summer Concert", "An outdoor concert.", Now.AddDays(30), Now.AddDays(30).AddHours(3), 50.00m, _timeProvider).Value;
        Assert.IsNotNull(evt);
        var result = await _eventRepository.AddAsync(evt);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedCustomerAsync(string email)
    {
        var customer = Customer.Create("Jane", "Doe", email).Value;
        Assert.IsNotNull(customer);
        var result = await _customerRepository.AddAsync(customer);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Guid> SeedReservationAsync(Guid seatId, Guid eventId, Guid customerId)
    {
        var seatLookup = await _seatRepository.GetByIdAsync(seatId);
        Assert.IsTrue(seatLookup.IsSuccess, string.Join("; ", seatLookup.ToFailureMessages()));
        var provisionResult = await _eventSeatRepository.ProvisionAsync(eventId, seatLookup.Value.VenueId);
        Assert.IsTrue(provisionResult.IsSuccess, string.Join("; ", provisionResult.ToFailureMessages()));

        var reservation = Reservation.Create(seatId, eventId, customerId, 50.00m, _timeProvider).Value;
        Assert.IsNotNull(reservation);
        var result = await _reservationRepository.AddAsync(reservation);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value.Id;
    }

    private async Task<Order> SeedPendingOrderAsync(Guid customerId, IReadOnlyCollection<Guid> reservationIds)
    {
        var order = Order.Create(customerId, reservationIds, _timeProvider).Value;
        Assert.IsNotNull(order);
        var result = await _orderRepository.AddAsync(order);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value;
    }

    // ============================================================
    // AddAsync / GetByIdAsync
    // ============================================================

    [TestMethod]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsOrderAndAllReservationIds()
    {
        // Arrange - proves the transaction actually wrote to both orders
        // and order_reservations, and that GetByIdAsync's second query
        // (LoadReservationIdsAsync) reconstructs the full set
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var reservationId1 = await SeedReservationAsync(seatId1, eventId, customerId);
        var reservationId2 = await SeedReservationAsync(seatId2, eventId, customerId);

        var order = Order.Create(customerId, [reservationId1, reservationId2], _timeProvider).Value;
        Assert.IsNotNull(order);

        // Act
        var addResult = await _orderRepository.AddAsync(order);
        var getResult = await _orderRepository.GetByIdAsync(order.Id);

        // Assert
        Assert.IsTrue(addResult.IsSuccess, string.Join("; ", addResult.ToFailureMessages()));
        Assert.IsTrue(getResult.IsSuccess, string.Join("; ", getResult.ToFailureMessages()));
        Assert.AreEqual(order.Id, getResult.Value.Id);
        Assert.AreEqual(customerId, getResult.Value.CustomerId);
        Assert.AreEqual(OrderStatus.Pending, getResult.Value.Status);
        Assert.IsNull(getResult.Value.ConfirmationNumber);
        CollectionAssert.AreEquivalent(
            new[] { reservationId1, reservationId2 },
            getResult.Value.ReservationIds.ToList());
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenOrderDoesNotExist_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _orderRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // GetByConfirmationNumberAsync
    // ============================================================

    [TestMethod]
    public async Task GetByConfirmationNumberAsync_WhenOrderIsCompleted_ReturnsMatchingOrderWithReservationIds()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);

        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        Assert.IsNotNull(completed.ConfirmationNumber);
        await ConfirmReservationAsync(reservationId);
        await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber);

        // Act
        var result = await _orderRepository.GetByConfirmationNumberAsync(completed.ConfirmationNumber);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.AreEqual(order.Id, result.Value.Id);
        CollectionAssert.AreEquivalent(new[] { reservationId }, result.Value.ReservationIds.ToList());
    }

    [TestMethod]
    public async Task GetByConfirmationNumberAsync_WhenNoMatch_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _orderRepository.GetByConfirmationNumberAsync("ZZZZ-ZZZZ");

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // GetByCustomerIdAsync
    // ============================================================

    [TestMethod]
    public async Task GetByCustomerIdAsync_WithMultipleOrdersEachHavingDifferentReservationSets_GroupsReservationIdsCorrectly()
    {
        // Arrange - the critical test for the left-join + GroupBy fix from
        // several sessions back: proves rows from two different orders,
        // each spanning multiple reservation rows, don't bleed into each
        // other's grouped ReservationIds
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var otherCustomerId = await SeedCustomerAsync("john.smith@example.com");

        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var seatId3 = await SeedSeatAsync(venueId, "A", 1, 3);

        var reservationId1 = await SeedReservationAsync(seatId1, eventId, customerId);
        var reservationId2 = await SeedReservationAsync(seatId2, eventId, customerId);
        var reservationId3 = await SeedReservationAsync(seatId3, eventId, otherCustomerId);

        var order1 = await SeedPendingOrderAsync(customerId, [reservationId1, reservationId2]);
        await SeedPendingOrderAsync(otherCustomerId, [reservationId3]); // different customer, must be excluded

        // Act
        var result = await _orderRepository.GetByCustomerIdAsync(customerId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.HasCount(1, result.Value);
        Assert.AreEqual(order1.Id, result.Value[0].Id);
        CollectionAssert.AreEquivalent(new[] { reservationId1, reservationId2 }, result.Value[0].ReservationIds.ToList());
    }

    [TestMethod]
    public async Task GetByCustomerIdAsync_WhenNoOrdersExist_ReturnsEmptyList()
    {
        // Arrange
        var customerId = await SeedCustomerAsync("jane.doe@example.com");

        // Act
        var result = await _orderRepository.GetByCustomerIdAsync(customerId);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsEmpty(result.Value);
    }

    // ============================================================
    // TryCompleteAsync
    // ============================================================

    [TestMethod]
    public async Task TryCompleteAsync_WhenAllReservationsConfirmed_ReturnsTrueAndPersistsStatusAndConfirmationNumber()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId);

        // Act
        var completeResult = await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);
        var getResult = await _orderRepository.GetByIdAsync(order.Id);

        // Assert
        Assert.IsTrue(completeResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(OrderStatus.Completed, getResult.Value.Status);
        Assert.AreEqual(completed.ConfirmationNumber, getResult.Value.ConfirmationNumber);
    }

    [TestMethod]
    public async Task TryCompleteAsync_WhenAnyReservationIsNotConfirmed_ReturnsFalse_AndLeavesOrderPending()
    {
        // Arrange - the pivot's own invariant check, and the whole reason the
        // resumable redesign is safe: two reservations, only one confirmed -
        // the pivot must refuse until every reservation it covers is Confirmed
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var reservationId1 = await SeedReservationAsync(seatId1, eventId, customerId);
        var reservationId2 = await SeedReservationAsync(seatId2, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId1, reservationId2]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId1);
        // reservationId2 stays Held - deliberately not confirmed

        // Act
        var completeResult = await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);
        var getResult = await _orderRepository.GetByIdAsync(order.Id);

        // Assert
        Assert.IsTrue(completeResult.IsSuccess, string.Join("; ", completeResult.ToFailureMessages()));
        Assert.IsFalse(completeResult.Value);
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(OrderStatus.Pending, getResult.Value.Status);
        Assert.IsNull(getResult.Value.ConfirmationNumber);
    }

    [TestMethod]
    public async Task TryCompleteAsync_WhenAllConfirmedAfterARetry_SucceedsOnTheSecondAttempt()
    {
        // Arrange - simulates CompleteOrder's exact resumability scenario: the
        // first attempt confirms one reservation then "crashes" before the
        // pivot runs; a retry confirms the rest and the pivot then succeeds
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId1 = await SeedSeatAsync(venueId, "A", 1, 1);
        var seatId2 = await SeedSeatAsync(venueId, "A", 1, 2);
        var reservationId1 = await SeedReservationAsync(seatId1, eventId, customerId);
        var reservationId2 = await SeedReservationAsync(seatId2, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId1, reservationId2]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId1);

        // Act - first attempt: reservationId2 still Held, pivot must refuse
        var firstAttempt = await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);

        // "Retry": the rest of the reservations finish confirming
        await ConfirmReservationAsync(reservationId2);
        var secondAttempt = await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);
        var getResult = await _orderRepository.GetByIdAsync(order.Id);

        // Assert
        Assert.IsTrue(firstAttempt is { IsSuccess: true, Value: false });
        Assert.IsTrue(secondAttempt is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(OrderStatus.Completed, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryCompleteAsync_WhenAlreadyCompleted_ReturnsFalse()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId);
        await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);

        // Act
        var result = await _orderRepository.TryCompleteAsync(order.Id, "AAAA-AAAA");

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryCancelAsync
    // ============================================================

    [TestMethod]
    public async Task TryCancelAsync_WhenPending_ReturnsTrueAndPersistsCancelledStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);

        // Act
        var cancelResult = await _orderRepository.TryCancelAsync(order.Id);
        var getResult = await _orderRepository.GetByIdAsync(order.Id);

        // Assert
        Assert.IsTrue(cancelResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(OrderStatus.Cancelled, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryCancelAsync_WhenCompleted_ReturnsFalse()
    {
        // Arrange - Cancel is only valid from Pending, unlike Reservation's Cancel
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId);
        await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);

        // Act
        var result = await _orderRepository.TryCancelAsync(order.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryRefundAsync
    // ============================================================

    [TestMethod]
    public async Task TryRefundAsync_WhenCompleted_ReturnsTrueAndPersistsRefundedStatus()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId);
        await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);

        // Act
        var refundResult = await _orderRepository.TryRefundAsync(order.Id);
        var getResult = await _orderRepository.GetByIdAsync(order.Id);

        // Assert
        Assert.IsTrue(refundResult is { IsSuccess: true, Value: true });
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual(OrderStatus.Refunded, getResult.Value.Status);
    }

    [TestMethod]
    public async Task TryRefundAsync_WhenPending_ReturnsFalse()
    {
        // Arrange - Refund is only valid from Completed
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);

        // Act
        var result = await _orderRepository.TryRefundAsync(order.Id);

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.IsFalse(result.Value);
    }

    // ============================================================
    // TryCancelAsync concurrency
    // ============================================================

    [TestMethod]
    public async Task TryCancelAsync_WhenTwentyFiveRequestsRaceForTheSameOrder_ExactlyOneSucceeds()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ => _orderRepository.TryCancelAsync(order.Id)));

        // Assert
        Assert.IsTrue(results.All(r => r.IsSuccess));
        Assert.AreEqual(1, results.Count(r => r.Value));

        var finalState = await _orderRepository.GetByIdAsync(order.Id);
        Assert.IsNotNull(finalState.Value);
        Assert.AreEqual(OrderStatus.Cancelled, finalState.Value.Status);
    }

    // ============================================================
    // TryRefundAsync concurrency
    // ============================================================

    [TestMethod]
    public async Task TryRefundAsync_WhenTwentyFiveRequestsRaceForTheSameOrder_ExactlyOneSucceeds()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);
        var completed = order.Complete().Value;
        Assert.IsNotNull(completed);
        await ConfirmReservationAsync(reservationId);
        await _orderRepository.TryCompleteAsync(order.Id, completed.ConfirmationNumber!);

        // Act
        var results = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ => _orderRepository.TryRefundAsync(order.Id)));

        // Assert
        Assert.IsTrue(results.All(r => r.IsSuccess));
        Assert.AreEqual(1, results.Count(r => r.Value));

        var finalState = await _orderRepository.GetByIdAsync(order.Id);
        Assert.IsNotNull(finalState.Value);
        Assert.AreEqual(OrderStatus.Refunded, finalState.Value.Status);
    }

    // ============================================================
    // TryCompleteAsync concurrency
    // ============================================================

    [TestMethod]
    public async Task TryCompleteAsync_WhenTwentyFiveRequestsRaceForTheSameOrder_ExactlyOneSucceeds()
    {
        // Arrange - all reservations already Confirmed, mirroring a legitimate
        // retry racing a genuine duplicate-submit; only one write can land
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);
        var order = await SeedPendingOrderAsync(customerId, [reservationId]);
        await ConfirmReservationAsync(reservationId);
        var confirmationNumbers = Enumerable.Range(0, 25).Select(_ => Guid.NewGuid().ToString("N")[..16]).ToArray();

        // Act
        var results = await Task.WhenAll(
            confirmationNumbers.Select(number => _orderRepository.TryCompleteAsync(order.Id, number)));

        // Assert
        Assert.IsTrue(results.All(r => r.IsSuccess));
        Assert.AreEqual(1, results.Count(r => r.Value));

        var finalState = await _orderRepository.GetByIdAsync(order.Id);
        Assert.IsNotNull(finalState.Value);
        Assert.AreEqual(OrderStatus.Completed, finalState.Value.Status);
        CollectionAssert.Contains(confirmationNumbers, finalState.Value.ConfirmationNumber);
    }

    // ============================================================
    // AddAsync concurrency
    // ============================================================

    [TestMethod]
    public async Task AddAsync_WhenTwoOrdersClaimTheSameReservationConcurrently_ExactlyOneSucceeds_AndTheLoserLeavesNoOrphanedOrder()
    {
        // Arrange
        var venueId = await SeedVenueAsync();
        var eventId = await SeedEventAsync(venueId);
        var customerId = await SeedCustomerAsync("jane.doe@example.com");
        var seatId = await SeedSeatAsync(venueId, "A", 1, 1);
        var reservationId = await SeedReservationAsync(seatId, eventId, customerId);

        var order1 = Order.Create(customerId, [reservationId], _timeProvider).Value;
        var order2 = Order.Create(customerId, [reservationId], _timeProvider).Value;
        Assert.IsNotNull(order1);
        Assert.IsNotNull(order2);

        // Act
        var results = await Task.WhenAll(
            _orderRepository.AddAsync(order1),
            _orderRepository.AddAsync(order2));

        // Assert - exactly one of the two inserts succeeds
        Assert.AreEqual(1, results.Count(r => r.IsSuccess));
        Assert.AreEqual(1, results.Count(r => r.IsFailure));

        var winner = results[0].IsSuccess ? order1 : order2;
        var loser = results[0].IsSuccess ? order2 : order1;

        var winnerLookup = await _orderRepository.GetByIdAsync(winner.Id);
        var loserLookup = await _orderRepository.GetByIdAsync(loser.Id);

        Assert.IsTrue(winnerLookup.IsSuccess);
        Assert.IsTrue(loserLookup.IsFailure, "The losing order's insert must have rolled back entirely - no orphaned orders row.");
        CollectionAssert.Contains(loserLookup.Errors.ToList(), RepositoryErrors.NotFound);
    }
}