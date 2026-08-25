using EventReservation.Application.Abstractions;
using EventReservation.Application.Features.Orders;
using EventReservation.Application.Features.Reservations;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Orders;

[TestClass]
public class CompleteOrderCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId1 = Guid.NewGuid();
    private static readonly Guid ReservationId2 = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private IDispatcher _dispatcher = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CompleteOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _dispatcher = Substitute.For<IDispatcher>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CompleteOrderCommandHandler(_orderRepository, _dispatcher);
    }

    private Order CreatePendingOrder() =>
        Order.Create(CustomerId, [ReservationId1, ReservationId2], _timeProvider).Value!;

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenAllReservationsConfirm_ReturnsSuccessWithConfirmationNumber_AndCallsTryCompleteAsync()
    {
        // Arrange
        var order = CreatePendingOrder();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _dispatcher.SendAsync<ConfirmReservationResult>(Arg.Any<ConfirmReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new ConfirmReservationResult(((ConfirmReservationCommand)callInfo[0]).Id)));
        _orderRepository.TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Value.ConfirmationNumber));
        await _orderRepository.Received(1).TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneReservationFailsToConfirm_ReturnsFailure_AndNeverCallsTryCompleteAsync()
    {
        // Arrange - proves all-or-nothing: a single failed reservation
        // blocks the order from ever being marked Completed
        var order = CreatePendingOrder();
        var confirmError = new ResultError("TEST", "reservation no longer held");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _dispatcher.SendAsync<ConfirmReservationResult>(
                Arg.Is<ConfirmReservationCommand>(c => c.Id == ReservationId1), Arg.Any<CancellationToken>())
            .Returns(Success(new ConfirmReservationResult(ReservationId1)));
        _dispatcher.SendAsync<ConfirmReservationResult>(
                Arg.Is<ConfirmReservationCommand>(c => c.Id == ReservationId2), Arg.Any<CancellationToken>())
            .Returns(Failure<ConfirmReservationResult>(confirmError));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), confirmError);
        await _orderRepository.DidNotReceive().TryCompleteAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOrderIsNotPending_ReturnsFailureWithCannotCompleteError_AndNeverCallsDispatcher()
    {
        // Arrange
        var order = CreatePendingOrder();
        order.Cancel();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotComplete);
        await _dispatcher.DidNotReceive().SendAsync<ConfirmReservationResult>(Arg.Any<ConfirmReservationCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryCompleteAsyncFails_ReturnsFailureWithOrderUpdateFailedError()
    {
        // Arrange
        var order = CreatePendingOrder();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _dispatcher.SendAsync<ConfirmReservationResult>(Arg.Any<ConfirmReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new ConfirmReservationResult(((ConfirmReservationCommand)callInfo[0]).Id)));
        _orderRepository.TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CompleteOrderCommandHandler.Errors.OrderUpdateFailed);
    }
}