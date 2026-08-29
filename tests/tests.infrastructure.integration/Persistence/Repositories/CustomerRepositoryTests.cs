using EventReservation.Application.Interfaces;
using EventReservation.Domain.Models;
using EventReservation.Infrastructure.Persistence;
using EventReservation.Infrastructure.Persistence.Repositories;

namespace EventReservation.Tests.Infrastructure.Integration.Persistence.Repositories;

[TestClass]
public class CustomerRepositoryTests : IntegrationTestBase
{
    private ICustomerRepository _customerRepository = null!;

    [TestInitialize]
    public void Setup()
    {
        _customerRepository = new CustomerRepository(ConnectionFactory);
    }

    private async Task<Customer> SeedCustomerAsync(string email = "jane.doe@example.com")
    {
        var customer = Customer.Create("Jane", "Doe", email).Value;
        Assert.IsNotNull(customer);
        var result = await _customerRepository.AddAsync(customer);
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        return result.Value;
    }

    // ============================================================
    // AddAsync / GetByIdAsync
    // ============================================================

    [TestMethod]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsAllFieldsCorrectly()
    {
        // Arrange
        var customer = Customer.Create("Jane", "Doe", "jane.doe@example.com").Value;
        Assert.IsNotNull(customer);

        // Act
        var addResult = await _customerRepository.AddAsync(customer);
        var getResult = await _customerRepository.GetByIdAsync(customer.Id);

        // Assert
        Assert.IsTrue(addResult.IsSuccess, string.Join("; ", addResult.ToFailureMessages()));
        Assert.IsTrue(getResult.IsSuccess, string.Join("; ", getResult.ToFailureMessages()));
        Assert.AreEqual(customer.Id, getResult.Value.Id);
        Assert.AreEqual("Jane", getResult.Value.FirstName);
        Assert.AreEqual("Doe", getResult.Value.LastName);
        Assert.AreEqual("jane.doe@example.com", getResult.Value.Email);
    }

    [TestMethod]
    public async Task GetByIdAsync_WhenCustomerDoesNotExist_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _customerRepository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    [TestMethod]
    public async Task AddAsync_WithDuplicateEmail_ReturnsDuplicateRecordFailure()
    {
        // Arrange
        await SeedCustomerAsync("jane.doe@example.com");
        var duplicate = Customer.Create("Janet", "Smith", "jane.doe@example.com").Value;
        Assert.IsNotNull(duplicate);

        // Act
        var result = await _customerRepository.AddAsync(duplicate);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), DatabaseExceptionMapper.Errors.DuplicateRecord);
    }

    // ============================================================
    // GetByEmailAsync
    // ============================================================

    [TestMethod]
    public async Task GetByEmailAsync_WhenCustomerExists_ReturnsMatchingCustomer()
    {
        // Arrange
        var customer = await SeedCustomerAsync("jane.doe@example.com");

        // Act
        var result = await _customerRepository.GetByEmailAsync("jane.doe@example.com");

        // Assert
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.ToFailureMessages()));
        Assert.AreEqual(customer.Id, result.Value.Id);
    }

    [TestMethod]
    public async Task GetByEmailAsync_WhenNoMatch_ReturnsNotFoundFailure()
    {
        // Act
        var result = await _customerRepository.GetByEmailAsync("nobody@example.com");

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    // ============================================================
    // UpdateAsync
    // ============================================================

    [TestMethod]
    public async Task UpdateAsync_WithValidCustomer_PersistsChangedFields()
    {
        // Arrange
        var customer = await SeedCustomerAsync("jane.doe@example.com");
        var renamed = customer.ChangeFirstName("Janet").Value;
        Assert.IsNotNull(renamed);
        var relastnamed = renamed.ChangeLastName("Smith").Value;
        Assert.IsNotNull(relastnamed);
        var updated = relastnamed.ChangeEmail("janet.smith@example.com").Value;
        Assert.IsNotNull(updated);

        // Act
        var updateResult = await _customerRepository.UpdateAsync(updated);
        var getResult = await _customerRepository.GetByIdAsync(customer.Id);

        // Assert
        Assert.IsTrue(updateResult.IsSuccess, string.Join("; ", updateResult.ToFailureMessages()));
        Assert.IsNotNull(getResult.Value);
        Assert.AreEqual("Janet", getResult.Value.FirstName);
        Assert.AreEqual("Smith", getResult.Value.LastName);
        Assert.AreEqual("janet.smith@example.com", getResult.Value.Email);
    }

    [TestMethod]
    public async Task UpdateAsync_WhenCustomerDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var customer = Customer.Create("Ghost", "Customer", "ghost@example.com").Value;
        Assert.IsNotNull(customer);

        // Act
        var result = await _customerRepository.UpdateAsync(customer);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), RepositoryErrors.NotFound);
    }

    [TestMethod]
    public async Task UpdateAsync_WhenNewEmailAlreadyTakenByAnotherCustomer_ReturnsDuplicateRecordFailure()
    {
        // Arrange
        await SeedCustomerAsync("taken@example.com");
        var customer = await SeedCustomerAsync("jane.doe@example.com");
        var updated = customer.ChangeEmail("taken@example.com").Value;
        Assert.IsNotNull(updated);

        // Act
        var result = await _customerRepository.UpdateAsync(updated);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), DatabaseExceptionMapper.Errors.DuplicateRecord);
    }
}