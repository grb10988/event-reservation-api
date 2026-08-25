using EventReservation.Application.Features.Orders;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Orders;

[TestClass]
public class GetOrderByIdQueryHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private GetOrderByIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _handler = new GetOrderByIdQueryHandler(_orderRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenOrderExists_ReturnsMappedResult()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Now);
        var order = Order.Create(CustomerId, [Guid.NewGuid()], timeProvider).Value;
        Assert.IsNotNull(order);
        _orderRepository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(Success(order));

        // Act
        var result = await _handler.HandleAsync(new GetOrderByIdQuery(order.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(order.Id, result.Value.Id);
        Assert.AreEqual(order.CustomerId, result.Value.CustomerId);
        Assert.AreEqual(order.Status, result.Value.Status);
    }

    [TestMethod]
    public async Task HandleAsync_WhenOrderDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _orderRepository.GetByIdAsync(orderId, Arg.Any<CancellationToken>()).Returns(Failure<Order>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetOrderByIdQuery(orderId));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}