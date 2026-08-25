using EventReservation.Application.Features.Orders;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Orders;

[TestClass]
public class CreateOrderCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId1 = Guid.NewGuid();
    private static readonly Guid ReservationId2 = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private IReservationRepository _reservationRepository = null!;
    private FakeTimeProvider _timeProvider = null!;
    private CreateOrderCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _reservationRepository = Substitute.For<IReservationRepository>();
        _timeProvider = new FakeTimeProvider(Now);
        _handler = new CreateOrderCommandHandler(_orderRepository, _reservationRepository, _timeProvider);
    }

    private Reservation CreateHeldReservationFor(Guid customerId) =>
        Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), customerId, 50.00m, _timeProvider).Value!;

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenAllReservationsAreOwnedAndHeld_ReturnsSuccessAndCallsAddAsync()
    {
        // Arrange
        var reservation1 = CreateHeldReservationFor(CustomerId);
        var reservation2 = CreateHeldReservationFor(CustomerId);
        var command = new CreateOrderCommand(CustomerId, [reservation1.Id, reservation2.Id]);

        _reservationRepository.GetByIdAsync(reservation1.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation1));
        _reservationRepository.GetByIdAsync(reservation2.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation2));
        _orderRepository.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Order>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CustomerId, result.Value.CustomerId);
        await _orderRepository.Received(1).AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneReservationBelongsToAnotherCustomer_ReturnsFailure_AndNeverCallsAddAsync()
    {
        // Arrange
        var reservation1 = CreateHeldReservationFor(CustomerId);
        var reservation2 = CreateHeldReservationFor(Guid.NewGuid()); // different customer
        var command = new CreateOrderCommand(CustomerId, [reservation1.Id, reservation2.Id]);

        _reservationRepository.GetByIdAsync(reservation1.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation1));
        _reservationRepository.GetByIdAsync(reservation2.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation2));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CreateOrderCommandHandler.Errors.ReservationNotOwnedOrHeld);
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenOneReservationIsNotHeld_ReturnsFailure_AndNeverCallsAddAsync()
    {
        // Arrange
        var reservation1 = CreateHeldReservationFor(CustomerId);
        var reservation2 = CreateHeldReservationFor(CustomerId);
        reservation2.Confirm();
        var command = new CreateOrderCommand(CustomerId, [reservation1.Id, reservation2.Id]);

        _reservationRepository.GetByIdAsync(reservation1.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation1));
        _reservationRepository.GetByIdAsync(reservation2.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation2));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), CreateOrderCommandHandler.Errors.ReservationNotOwnedOrHeld);
        await _orderRepository.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenBothReservationsFailVerification_StillOnlyReportsOneError_AndChecksBoth()
    {
        // Arrange - proves every reservation is actually checked (via
        // Received calls below), even though Combine's non-generic Result
        // overload doesn't accumulate duplicate copies of the same error
        var reservation1 = CreateHeldReservationFor(Guid.NewGuid());
        var reservation2 = CreateHeldReservationFor(Guid.NewGuid());
        var command = new CreateOrderCommand(CustomerId, [reservation1.Id, reservation2.Id]);

        _reservationRepository.GetByIdAsync(reservation1.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation1));
        _reservationRepository.GetByIdAsync(reservation2.Id, Arg.Any<CancellationToken>()).Returns(Success(reservation2));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        await _reservationRepository.Received(1).GetByIdAsync(reservation1.Id, Arg.Any<CancellationToken>());
        await _reservationRepository.Received(1).GetByIdAsync(reservation2.Id, Arg.Any<CancellationToken>());
    }
}