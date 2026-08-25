using EventReservation.Application.Features.Customers;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Customers;

[TestClass]
public class GetCustomerByIdQueryHandlerTests
{
    private ICustomerRepository _customerRepository = null!;
    private GetCustomerByIdQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _customerRepository = Substitute.For<ICustomerRepository>();
        _handler = new GetCustomerByIdQueryHandler(_customerRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WhenCustomerExists_ReturnsMappedResult()
    {
        // Arrange
        var customer = Customer.Create("Jane", "Doe", "jane.doe@example.com").Value;
        Assert.IsNotNull(customer);
        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(Success(customer));

        // Act
        var result = await _handler.HandleAsync(new GetCustomerByIdQuery(customer.Id));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(customer.Id, result.Value.Id);
        Assert.AreEqual(customer.Email, result.Value.Email);
    }

    [TestMethod]
    public async Task HandleAsync_WhenCustomerDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(Failure<Customer>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetCustomerByIdQuery(customerId));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}