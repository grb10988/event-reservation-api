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
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private IDispatcher _dispatcher = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CompleteOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _reservationRepository = Substitute.For<IReservationRepository>();
        _dispatcher = Substitute.For<IDispatcher>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CompleteOrderCommandHandler(_orderRepository, _reservationRepository, _dispatcher);
    }

    private Reservation CreateHeldReservation() =>
        Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), CustomerId, 50.00m, _timeProvider).Value!;

    private Order CreatePendingOrder(params Reservation[] reservations) =>
        Order.Create(CustomerId, reservations.Select(r => r.Id).ToArray(), _timeProvider).Value!;

    private void StubReservation(Reservation reservation) =>
        _reservationRepository.GetByIdAsync(reservation.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation));

    private void StubDispatcherConfirmsAnything() =>
        _dispatcher.SendAsync<ConfirmReservationResult>(Arg.Any<ConfirmReservationCommand>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(new ConfirmReservationResult(((ConfirmReservationCommand)callInfo[0]).Id)));

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenBothReservationsHeld_ConfirmsEachAndCompletes()
    {
        // Arrange
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        StubReservation(reservation1);
        StubReservation(reservation2);
        StubDispatcherConfirmsAnything();
        _orderRepository.TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.Value.ConfirmationNumber));
        await _dispatcher.Received(1).SendAsync<ConfirmReservationResult>(
            Arg.Is<ConfirmReservationCommand>(c => c.Id == reservation1.Id), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendAsync<ConfirmReservationResult>(
            Arg.Is<ConfirmReservationCommand>(c => c.Id == reservation2.Id), Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneAlreadyConfirmed_OnlyConfirmsTheRemaining_AndCompletes()
    {
        // Arrange - the resumability test: a retry after a partial failure
        // must skip the reservation that already confirmed, not re-attempt it
        var alreadyConfirmed = CreateHeldReservation();
        alreadyConfirmed.Confirm();
        var stillHeld = CreateHeldReservation();
        var order = CreatePendingOrder(alreadyConfirmed, stillHeld);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        StubReservation(alreadyConfirmed);
        StubReservation(stillHeld);
        StubDispatcherConfirmsAnything();
        _orderRepository.TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Success(true));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        await _dispatcher.DidNotReceive().SendAsync<ConfirmReservationResult>(
            Arg.Is<ConfirmReservationCommand>(c => c.Id == alreadyConfirmed.Id), Arg.Any<CancellationToken>());
        await _dispatcher.Received(1).SendAsync<ConfirmReservationResult>(
            Arg.Is<ConfirmReservationCommand>(c => c.Id == stillHeld.Id), Arg.Any<CancellationToken>());
        await _orderRepository.Received(1).TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenAnyReservationIsCancelledOrExpired_FailsFast_WithoutConfirmingAnything()
    {
        // Arrange
        var cancelled = CreateHeldReservation();
        cancelled.Cancel();
        var stillHeld = CreateHeldReservation();
        var order = CreatePendingOrder(cancelled, stillHeld);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        StubReservation(cancelled);
        StubReservation(stillHeld);

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CompleteOrderCommandHandler.Errors.ReservationNoLongerActive);
        await _dispatcher.DidNotReceive().SendAsync<ConfirmReservationResult>(Arg.Any<ConfirmReservationCommand>(), Arg.Any<CancellationToken>());
        await _orderRepository.DidNotReceive().TryCompleteAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneConfirmationLosesARace_ReturnsFailure_AndNeverCallsTryCompleteAsync()
    {
        // Arrange - proves a single failed confirmation blocks completion
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);
        var confirmError = new ResultError("TEST", "reservation no longer held");

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        StubReservation(reservation1);
        StubReservation(reservation2);
        _dispatcher.SendAsync<ConfirmReservationResult>(
                Arg.Is<ConfirmReservationCommand>(c => c.Id == reservation1.Id), Arg.Any<CancellationToken>())
            .Returns(Success(new ConfirmReservationResult(reservation1.Id)));
        _dispatcher.SendAsync<ConfirmReservationResult>(
                Arg.Is<ConfirmReservationCommand>(c => c.Id == reservation2.Id), Arg.Any<CancellationToken>())
            .Returns(Failure<ConfirmReservationResult>(confirmError));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), confirmError);
        await _orderRepository.DidNotReceive().TryCompleteAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenPivotLosesTheRace_ReturnsOrderNoLongerCompletableConflict()
    {
        // Arrange
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);

        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));
        StubReservation(reservation1);
        StubReservation(reservation2);
        StubDispatcherConfirmsAnything();
        _orderRepository.TryCompleteAsync(order.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Success(false));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CompleteOrderCommandHandler.Errors.OrderNoLongerCompletable);
        Assert.IsTrue(result.Errors.All(e => e.Category == ErrorCategory.Conflict));
    }

    [TestMethod]
    public async Task HandleAsync_WhenOrderIsNotPending_ReturnsFailureWithCannotCompleteError_AndNeverTouchesReservationsOrDispatcher()
    {
        // Arrange
        var reservation1 = CreateHeldReservation();
        var reservation2 = CreateHeldReservation();
        var order = CreatePendingOrder(reservation1, reservation2);
        order.Cancel();
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));

        // Act
        var result = await _handler.HandleAsync(new CompleteOrderCommand(order.Id));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Order.Errors.CannotComplete);
        await _reservationRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceive().SendAsync<ConfirmReservationResult>(Arg.Any<ConfirmReservationCommand>(), Arg.Any<CancellationToken>());
    }
}