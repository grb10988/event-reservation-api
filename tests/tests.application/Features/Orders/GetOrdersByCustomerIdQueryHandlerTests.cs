using EventReservation.Application.Features.Orders;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Orders;

[TestClass]
public class GetOrdersByCustomerQueryHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private GetOrdersByCustomerIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _handler = new GetOrdersByCustomerIdQueryHandler(_orderRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenOrdersExist_ReturnsMappedSummaries()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Now);
        var order = Order.Create(CustomerId, [Guid.NewGuid()], timeProvider).Value;
        Assert.IsNotNull(order);
        _orderRepository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Order>>([order]));

        // Act
        var result = await _handler.HandleAsync(new GetOrdersByCustomerIdQuery(CustomerId));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, result.Value.Count);
        Assert.AreEqual(order.Id, result.Value[0].Id);
    }

    [TestMethod]
    public async Task HandleAsync_WhenNoOrdersExist_ReturnsEmptyList()
    {
        // Arrange
        _orderRepository.GetByCustomerIdAsync(CustomerId, Arg.Any<CancellationToken>())
            .Returns(Success<IReadOnlyList<Order>>([]));

        // Act
        var result = await _handler.HandleAsync(new GetOrdersByCustomerIdQuery(CustomerId));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0, result.Value.Count);
    }
}