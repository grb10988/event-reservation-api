using EventReservation.Application.Features.Orders;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Orders;

[TestClass]
public class GetOrderByConfirmationNumberQueryHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private IOrderRepository _orderRepository = null!;
    private GetOrderByConfirmationNumberQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _orderRepository = Substitute.For<IOrderRepository>();
        _handler = new GetOrderByConfirmationNumberQueryHandler(_orderRepository);
    }

    private static string BuildValidConfirmationNumber()
    {
        var segment = new string(Order.ConfirmationCharacters[0], Order.ConfirmationSegmentLength);
        return string.Join('-', Enumerable.Repeat(segment, Order.ConfirmationSegmentCount));
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithWellFormedNumber_WhenOrderExists_ReturnsMappedResult()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider(Now);
        var order = Order.Create(CustomerId, [Guid.NewGuid()], timeProvider).Value;
        Assert.IsNotNull(order);
        var confirmationNumber = BuildValidConfirmationNumber();

        _orderRepository.GetByConfirmationNumberAsync(confirmationNumber, Arg.Any<CancellationToken>()).Returns(Success(order));

        // Act
        var result = await _handler.HandleAsync(new GetOrderByConfirmationNumberQuery(confirmationNumber));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(order.Id, result.Value.Id);
    }

    [TestMethod]
    public async Task HandleAsync_WithMalformedNumber_ReturnsFailureWithInvalidFormatError_AndNeverCallsRepository()
    {
        // Arrange - this is the test that proves IsValidConfirmationNumberFormat
        // is actually being enforced before the database is ever touched
        var command = new GetOrderByConfirmationNumberQuery("not-a-real-confirmation-number");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), GetOrderByConfirmationNumberQueryHandler.Errors.InvalidFormat);
        await _orderRepository.DidNotReceive().GetByConfirmationNumberAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithWellFormedNumber_WhenOrderDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var confirmationNumber = BuildValidConfirmationNumber();
        var notFoundError = new ResultError("TEST", "not found");
        _orderRepository.GetByConfirmationNumberAsync(confirmationNumber, Arg.Any<CancellationToken>()).Returns(Failure<Order>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetOrderByConfirmationNumberQuery(confirmationNumber));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}