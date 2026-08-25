using EventReservation.Application.Features.Customers;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Customers;

[TestClass]
public class UpdateCustomerCommandHandlerTests
{
    private ICustomerRepository _customerRepository = null!;
    private UpdateCustomerCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _customerRepository = Substitute.For<ICustomerRepository>();
        _handler = new UpdateCustomerCommandHandler(_customerRepository);
    }

    private Customer CreateValidCustomer()
    {
        var customer = Customer.Create("Jane", "Doe", "jane.doe@example.com").Value;
        Assert.IsNotNull(customer);
        return customer;
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessAndCallsUpdateAsync()
    {
        // Arrange
        var customer = CreateValidCustomer();
        var command = new UpdateCustomerCommand(customer.Id, "Janet", "Smith", "janet.smith@example.com");

        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(Success(customer));
        _customerRepository.UpdateAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Customer>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(command.FirstName, result.Value.FirstName);
        Assert.AreEqual(command.Email, result.Value.Email);
        await _customerRepository.Received(1).UpdateAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithMultipleInvalidFields_AccumulatesAllErrors_AndNeverCallsUpdateAsync()
    {
        // Arrange - proves Combine's accumulation, same as Event/Venue's update handlers
        var customer = CreateValidCustomer();
        var command = new UpdateCustomerCommand(customer.Id, "", "", "not-an-email");

        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(Success(customer));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyFirstName);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyLastName);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.InvalidEmail);
        await _customerRepository.DidNotReceive().UpdateAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenNewEmailAlreadyExists_PropagatesDuplicateRecordFailure()
    {
        // Arrange
        var customer = CreateValidCustomer();
        var command = new UpdateCustomerCommand(customer.Id, "Jane", "Doe", "taken@example.com");
        var duplicateError = new ResultError("DATABASE", "A record with the same unique value(s) already exists.");

        _customerRepository.GetByIdAsync(customer.Id, Arg.Any<CancellationToken>()).Returns(Success(customer));
        _customerRepository.UpdateAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(Failure<Customer>(duplicateError));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), duplicateError);
    }

    [TestMethod]
    public async Task HandleAsync_WhenCustomerNotFound_PropagatesFailure_AndNeverCallsUpdateAsync()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var notFoundError = new ResultError("TEST", "not found");
        _customerRepository.GetByIdAsync(customerId, Arg.Any<CancellationToken>()).Returns(Failure<Customer>(notFoundError));

        // Act
        var result = await _handler.HandleAsync(new UpdateCustomerCommand(customerId, "Jane", "Doe", "jane.doe@example.com"));

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), notFoundError);
        await _customerRepository.DidNotReceive().UpdateAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }
}