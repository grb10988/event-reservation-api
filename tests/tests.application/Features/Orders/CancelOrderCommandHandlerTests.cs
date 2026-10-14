using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Orders;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Orders;

[TestClass]
public class CancelOrderCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private IDispatcher _dispatcher = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CancelOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _reservationRepository = Substitute.For<IReservationRepository>();
        _dispatcher = Substitute.For<IDispatcher>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CancelOrderCommandHandler(_orderRepository, _reservationRepository, _dispatcher);
    }

    private Reservation CreateHeldReservation() =>
        Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), CustomerId, 50.00m, _timeProvider).Value!;

    private Order CreatePendingOrder(params Reservation[] reservations) =>
        Order.Create(CustomerId, reservations.Select(r => r.Id).ToArray(), _timeProvider).Value!;

    private void StubReservation(Reservation reservation) =>
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

    private void StubDispatcherCancelsAnything() =>
        _dispatcher.SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new CancelReservationResult(((CancelReservationCommand)callInfo[0]).Id)));

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenClaimWins_CancelsEachActiveReservation_ReturnsSuccess()
    {
        // Arrange
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _orderRepository.TryCancelAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubReservation(reservation1);
        StubReservation(reservation2);
        StubDispatcherCancelsAnything();

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _orderRepository.Received(1).TryCancelAsync(order.Id, Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendAsync<CancelReservationResult>(
            Arg.Is<CancelReservationCommand>(c => c.Id == reservation1.Id), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendAsync<CancelReservationResult>(
            Arg.Is<CancelReservationCommand>(c => c.Id == reservation2.Id), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenClaimIsLost_ReturnsFailure_AndNeverTouchesAnyReservation()
    {
        // Arrange - the behavioural proof of claim-first: losing the order's
        // own CAS must mean nothing downstream is ever touched
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _orderRepository.TryCancelAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CancelOrderCommandHandler.Errors.OrderNoLongerPending);
        await _reservationRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOrderIsNotPending_ReturnsFailureWithCannotCancelError_AndNeverCallsTryCancelAsync()
    {
        // Arrange
        var reservation = CreateHeldReservation();
        var order = CreatePendingOrder(reservation);
        order.Complete();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotCancel);
        await _orderRepository.DidNotReceive().TryCancelAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneReservationWasAlreadyCancelled_SkipsIt_AndStillSucceeds()
    {
        // Arrange
        var alreadyCancelled = CreateHeldReservation();
        alreadyCancelled.Cancel();
        var stillHeld = CreateHeldReservation();
        var order = CreatePendingOrder(alreadyCancelled, stillHeld);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _orderRepository.TryCancelAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubReservation(alreadyCancelled);
        StubReservation(stillHeld);
        StubDispatcherCancelsAnything();

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(
            Arg.Is<CancelReservationCommand>(c => c.Id == alreadyCancelled.Id), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendAsync<CancelReservationResult>(
            Arg.Is<CancelReservationCommand>(c => c.Id == stillHeld.Id), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenAReservationCancelFails_ReturnsFailure_AfterTheClaimWasMade()
    {
        // Arrange - proves the roll-forward contract: the claim already
        // committed by the time a downstream cancellation can fail
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);
        var cancelError = new ResultError("TEST", "reservation already expired");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _orderRepository.TryCancelAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(true));
        StubReservation(reservation1);
        StubReservation(reservation2);
        _dispatcher.SendAsync<CancelReservationResult>(
                Arg.Is<CancelReservationCommand>(c => c.Id == reservation1.Id), Arg.Any<CancellationToken>())
            .Returns(Success(new CancelReservationResult(reservation1.Id)));
        _dispatcher.SendAsync<CancelReservationResult>(
                Arg.Is<CancelReservationCommand>(c => c.Id == reservation2.Id), Arg.Any<CancellationToken>())
            .Returns(Failure<CancelReservationResult>(cancelError));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), cancelError);
        await _orderRepository.Received(1).TryCancelAsync(order.Id, Arg.Any<CancellationToken>());
    }
}