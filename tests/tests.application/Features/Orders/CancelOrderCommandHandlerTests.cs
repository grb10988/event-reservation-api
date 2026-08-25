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
    private static readonly Guid ReservationId1 = Guid.NewGuid();
    private static readonly Guid ReservationId2 = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private IDispatcher _dispatcher = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CancelOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _dispatcher = Substitute.For<IDispatcher>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CancelOrderCommandHandler(_orderRepository, _dispatcher);
    }

    private Order CreatePendingOrder() =>
        Order.Create(CustomerId, [ReservationId1, ReservationId2], _timeProvider).Value!;

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenAllReservationsCancel_ReturnsSuccess_AndCallsTryCancelAsync()
    {
        // Arrange
        var order = CreatePendingOrder();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _dispatcher.SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new CancelReservationResult(((CancelReservationCommand)callInfo[0]).Id)));
        _orderRepository.TryCancelAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _orderRepository.Received(1).TryCancelAsync(order.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneReservationFailsToCancel_ReturnsFailure_AndNeverCallsTryCancelAsync()
    {
        // Arrange
        var order = CreatePendingOrder();
        var cancelError = new ResultError("TEST", "reservation already expired");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _dispatcher.SendAsync<CancelReservationResult>(
                Arg.Is<CancelReservationCommand>(c => c.Id == ReservationId1), Arg.Any<CancellationToken>())
            .Returns(Success(new CancelReservationResult(ReservationId1)));
        _dispatcher.SendAsync<CancelReservationResult>(
                Arg.Is<CancelReservationCommand>(c => c.Id == ReservationId2), Arg.Any<CancellationToken>())
            .Returns(Failure<CancelReservationResult>(cancelError));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), cancelError);
        await _orderRepository.DidNotReceive().TryCancelAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOrderIsNotPending_ReturnsFailureWithCannotCancelError_AndNeverCallsDispatcher()
    {
        // Arrange
        var order = CreatePendingOrder();
        order.Complete();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotCancel);
        await _dispatcher.DidNotReceive().SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenTryCancelAsyncFails_ReturnsFailureWithOrderUpdateFailedError()
    {
        // Arrange
        var order = CreatePendingOrder();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        _dispatcher.SendAsync<CancelReservationResult>(Arg.Any<CancelReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new CancelReservationResult(((CancelReservationCommand)callInfo[0]).Id)));
        _orderRepository.TryCancelAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CancelOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CancelOrderCommandHandler.Errors.OrderUpdateFailed);
    }
}