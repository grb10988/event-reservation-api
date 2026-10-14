using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Events;
using EventReservation.Application.Features.Orders;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Events;

[TestClass]
public class CancelEventCommandHandlerTests
{
    private static readonly Guid VenueId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IEventRepository _eventRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private IOrderRepository _orderRepository = null!;
    private IDispatcher _dispatcher = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CancelEventCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _eventRepository = Substitute.For<IEventRepository>();
        _reservationRepository = Substitute.For<IReservationRepository>();
        _orderRepository = Substitute.For<IOrderRepository>();
        _dispatcher = Substitute.For<IDispatcher>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CancelEventCommandHandler(_eventRepository, _reservationRepository, _orderRepository, _dispatcher);
    }

    private Event CreateDraftEvent()
    {
        var evt = Event.Create(VenueId, "Summer Concert", "An outdoor concert.", Now.AddDays(30), Now.AddDays(30).AddHours(3), 50.00m, _timeProvider).Value;
        Assert.IsNotNull(evt);
        return evt;
    }

    private Reservation CreateHeldReservation(Guid eventId) =>
        Reservation.Create(Guid.NewGuid(), eventId, CustomerId, 50.00m, _timeProvider).Value!;

    private void StubNoActiveReservations(Guid eventId) =>
        _reservationRepository.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Success((IReadOnlyList<Reservation>)Array.Empty<Reservation>()));

    private void StubActiveReservations(Guid eventId, params Reservation[] reservations) =>
        _reservationRepository.GetByEventIdAsync(eventId, Arg.Any<CancellationToken>())
            .Returns(Success((IReadOnlyList<Reservation>)reservations));

    private void StubUnowned(Reservation reservation) =>
        _orderRepository.GetByReservationIdAsync(reservation.Id, Arg.Any<CancellationToken>())
            .Returns(Failure<Order>(new ResultError("TEST", "no order found for this reservation")));

    private void StubOwnedBy(Reservation reservation, Order order) =>
        _orderRepository.GetByReservationIdAsync(reservation.Id, Arg.Any<CancellationToken>())
            .Returns(Success(order));

    private void StubDispatcherHandlesEverything()
    {
        _dispatcher.SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new CancelReservationResult(((CancelReservationCommand)callInfo[0]).Id)));
        _dispatcher.SendAsync<CancelOrderResult>(Arg.Any<CancelOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new CancelOrderResult(((CancelOrderCommand)callInfo[0]).Id)));
        _dispatcher.SendAsync<RefundOrderResult>(Arg.Any<RefundOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new RefundOrderResult(((RefundOrderCommand)callInfo[0]).Id)));
    }

    // ============================================================
    // HandleAsync - claiming the event
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenDraftWithNoActiveReservations_ReturnsSuccess()
    {
        // Arrange
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubNoActiveReservations(evt.Id);

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task HandleAsync_WhenPublished_ReturnsSuccess()
    {
        // Arrange - Cancel is valid from either Draft or Published
        var evt = CreateDraftEvent();
        evt.Publish();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubNoActiveReservations(evt.Id);

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task HandleAsync_WhenAlreadyCancelled_ReturnsFailureWithCannotCancelError_AndNeverRunsTheCascade()
    {
        // Arrange
        var evt = CreateDraftEvent();
        evt.Cancel();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Event.Errors.CannotCancel);
        await _eventRepository.DidNotReceive().TryCancelAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _reservationRepository.DidNotReceive().GetByEventIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryCancelAsyncLosesTheRace_ReturnsFailureWithEventAlreadyCancelledError_AndNeverRunsTheCascade()
    {
        // Arrange - the claim-first guarantee: losing the event's own CAS
        // must mean the cascade never even looks at its reservations
        var evt = CreateDraftEvent();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CancelEventCommandHandler.Errors.EventAlreadyCancelled);
        await _reservationRepository.DidNotReceive().GetByEventIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ============================================================
    // HandleAsync - unwinding active reservations
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenReservationHasNoOrder_CancelsItDirectly()
    {
        // Arrange - held/confirmed but never checked out
        var evt = CreateDraftEvent();
        evt.Publish();
        var reservation = CreateHeldReservation(evt.Id);

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubActiveReservations(evt.Id, reservation);
        StubUnowned(reservation);
        StubDispatcherHandlesEverything();

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.Received(1).SendAsync<CancelReservationResult>(
            Arg.Is<CancelReservationCommand>(c => c.Id == reservation.Id), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelOrderResult>(Arg.Any<CancelOrderCommand>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<RefundOrderResult>(Arg.Any<RefundOrderCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationBelongsToAPendingOrder_DispatchesCancelOrder_NotADirectCancel()
    {
        // Arrange
        var evt = CreateDraftEvent();
        evt.Publish();
        var reservation = CreateHeldReservation(evt.Id);
        var order = Order.Create(CustomerId, [reservation.Id], _timeProvider).Value!;

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubActiveReservations(evt.Id, reservation);
        StubOwnedBy(reservation, order);
        StubDispatcherHandlesEverything();

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.Received(1).SendAsync<CancelOrderResult>(
            Arg.Is<CancelOrderCommand>(c => c.Id == order.Id), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(
            Arg.Is<CancelReservationCommand>(c => c.Id == reservation.Id), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<RefundOrderResult>(Arg.Any<RefundOrderCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenReservationBelongsToACompletedOrder_DispatchesRefundOrder()
    {
        // Arrange
        var evt = CreateDraftEvent();
        evt.Publish();
        var reservation = CreateHeldReservation(evt.Id);
        var order = Order.Create(CustomerId, [reservation.Id], _timeProvider).Value!;
        order.Complete();

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubActiveReservations(evt.Id, reservation);
        StubOwnedBy(reservation, order);
        StubDispatcherHandlesEverything();

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.Received(1).SendAsync<RefundOrderResult>(
            Arg.Is<RefundOrderCommand>(c => c.Id == order.Id), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelOrderResult>(Arg.Any<CancelOrderCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOrderIsAlreadyCancelledOrRefunded_DoesNothingForThatOrder()
    {
        // Arrange - the invariant we're trusting rather than re-verifying:
        // an already-terminal order's reservations should already be settled
        var evt = CreateDraftEvent();
        evt.Publish();
        var reservation = CreateHeldReservation(evt.Id);
        var order = Order.Create(CustomerId, [reservation.Id], _timeProvider).Value!;
        order.Cancel();

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubActiveReservations(evt.Id, reservation);
        StubOwnedBy(reservation, order);

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.DidNotReceive().SendAsync<CancelOrderResult>(Arg.Any<CancelOrderCommand>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<RefundOrderResult>(Arg.Any<RefundOrderCommand>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTwoReservationsBelongToTheSamePendingOrder_DispatchesCancelOrderExactlyOnce()
    {
        // Arrange - the dedup proof: an order covering several seats for
        // this event must not be settled once per reservation
        var evt = CreateDraftEvent();
        evt.Publish();
        var reservation1 = CreateHeldReservation(evt.Id);
        var reservation2 = CreateHeldReservation(evt.Id);
        var order = Order.Create(CustomerId, [reservation1.Id, reservation2.Id], _timeProvider).Value!;

        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubActiveReservations(evt.Id, reservation1, reservation2);
        StubOwnedBy(reservation1, order);
        StubOwnedBy(reservation2, order);
        StubDispatcherHandlesEverything();

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.Received(1).SendAsync<CancelOrderResult>(
            Arg.Is<CancelOrderCommand>(c => c.Id == order.Id), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenNoActiveReservationsExist_ReturnsSuccess_WithoutDispatchingAnything()
    {
        // Arrange - every reservation for this event is already Cancelled
        // or Expired; the cascade must degrade to a no-op, not error
        var evt = CreateDraftEvent();
        evt.Publish();
        _eventRepository.GetByIdAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(evt));
        _eventRepository.TryCancelAsync(evt.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubNoActiveReservations(evt.Id);

        // Act
        var result = await _handler.HandleAsync(new CancelEventCommand(evt.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelOrderResult>(Arg.Any<CancelOrderCommand>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<RefundOrderResult>(Arg.Any<RefundOrderCommand>(), Arg.Any<CancellationToken>());
    }
}