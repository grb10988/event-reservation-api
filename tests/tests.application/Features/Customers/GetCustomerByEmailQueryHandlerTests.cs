using EventReservation.Application.Features.Customers;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Customers;

[TestClass]
public class GetCustomerByEmailQueryHandlerTests
{
    private ICustomerRepository _customerRepository = null!;
    private GetCustomerByEmailQueryHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _customerRepository = Substitute.For<ICustomerRepository>();
        _handler = new GetCustomerByEmailQueryHandler(_customerRepository);
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
        _customerRepository.GetByEmailAsync(customer.Email, Arg.Any<CancellationToken>()).Returns(Success(customer));

        // Act
        var result = await _handler.HandleAsync(new GetCustomerByEmailQuery(customer.Email));

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(customer.Id, result.Value.Id);
    }

    [TestMethod]
    public async Task HandleAsync_WhenCustomerDoesNotExist_PropagatesFailure()
    {
        // Arrange
        var email = "nobody@example.com";
        var notFoundError = new ResultError("TEST", "not found");
        _customerRepository.GetByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(Failure<Customer>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new GetCustomerByEmailQuery(email));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
    }
}