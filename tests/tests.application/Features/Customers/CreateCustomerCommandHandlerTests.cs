using EventReservation.Application.Features.Customers;
using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using NSubstitute;

namespace EventReservation.Tests.Application.Features.Customers;

[TestClass]
public class CreateCustomerCommandHandlerTests
{
    private ICustomerRepository _customerRepository = null!;
    private CreateCustomerCommandHandler _handler = null!;

    [TestInitialize]
    public void Setup()
    {
        _customerRepository = Substitute.For<ICustomerRepository>();
        _handler = new CreateCustomerCommandHandler(_customerRepository);
    }

    // ============================================================
    // HandleAsync
    // ============================================================

    [TestMethod]
    public async Task HandleAsync_WithValidCommand_ReturnsSuccessAndCallsAddAsync()
    {
        // Arrange
        var command = new CreateCustomerCommand("Jane", "Doe", "jane.doe@example.com");
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Success(callInfo.Arg<Customer>()));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(command.Email, result.Value.Email);
        await _customerRepository.Received(1).AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WithInvalidEmail_ReturnsFailureWithInvalidEmailError_AndNeverCallsAddAsync()
    {
        // Arrange
        var command = new CreateCustomerCommand("Jane", "Doe", "not-an-email");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.InvalidEmail);
        await _customerRepository.DidNotReceive().AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task HandleAsync_WhenEmailAlreadyExists_PropagatesDuplicateRecordFailure()
    {
        // Arrange - the database-level uniqueness check, surfaced through
        // DatabaseExceptionTranslator, not a Domain-level rule
        var command = new CreateCustomerCommand("Jane", "Doe", "jane.doe@example.com");
        var duplicateError = new ResultError("DATABASE", "A record with the same unique value(s) already exists.");
        _customerRepository.AddAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns(Failure<Customer>(duplicateError));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), duplicateError);
    }
}