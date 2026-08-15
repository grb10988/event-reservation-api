using EventReservation.Domain.Models;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class CustomerTests
{
    private const string ValidFirstName = "Jane";
    private const string ValidLastName = "Doe";
    private const string ValidEmail = "jane.doe@example.com";

    // ============================================================
    // Create
    // ============================================================

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithExpectedValues()
    {
        // Act
        var result = Customer.Create(ValidFirstName, ValidLastName, ValidEmail);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidFirstName, result.Value.FirstName);
        Assert.AreEqual(ValidLastName, result.Value.LastName);
        Assert.AreEqual(ValidEmail, result.Value.Email);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidFirstName_ReturnsFailureWithEmptyFirstNameError(string? firstName)
    {
        // Act
        var result = Customer.Create(firstName!, ValidLastName, ValidEmail);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyFirstName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidLastName_ReturnsFailureWithEmptyLastNameError(string? lastName)
    {
        // Act
        var result = Customer.Create(ValidFirstName, lastName!, ValidEmail);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyLastName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithMissingEmail_ReturnsFailureWithEmptyEmailError(string? email)
    {
        // Act
        var result = Customer.Create(ValidFirstName, ValidLastName, email!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyEmail);
    }

    [TestMethod]
    [DataRow("not-an-email")]
    [DataRow("missing-at-sign.com")]
    [DataRow("@no-local-part.com")]
    public void Create_WithMalformedEmail_ReturnsFailureWithInvalidEmailError(string email)
    {
        // Act
        var result = Customer.Create(ValidFirstName, ValidLastName, email);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.InvalidEmail);
    }

    [TestMethod]
    public void Create_WithMissingFirstNameAndMalformedEmail_ReturnsBothErrors()
    {
        // Act - each field now validates independently; FirstName's absence
        // no longer suppresses Email's own format check
        var result = Customer.Create("", ValidLastName, "not-an-email");

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyFirstName);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.InvalidEmail);
    }

    [TestMethod]
    public void Create_WithAllFieldsMissing_AccumulatesOnlyPresenceErrors()
    {
        // Act - Email itself being empty still gates InvalidEmail via
        // ValidateEmail's own internal two-phase check
        var result = Customer.Create("", "", "");

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(3, result.Errors.Count);
        CollectionAssert.DoesNotContain(result.Errors.ToList(), Customer.Errors.InvalidEmail);
    }

    [TestMethod]
    public void Create_CalledTwiceWithSameInputs_GeneratesDifferentIds()
    {
        // Act
        var first = Customer.Create(ValidFirstName, ValidLastName, ValidEmail);
        var second = Customer.Create(ValidFirstName, ValidLastName, ValidEmail);

        // Assert
        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreNotEqual(first.Value.Id, second.Value.Id);
    }

    // ============================================================
    // ChangeFirstName
    // ============================================================

    [TestMethod]
    public void ChangeFirstName_WithValidName_ReturnsSuccessWithUpdatedFirstName()
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);
        const string newFirstName = "Janet";

        // Act
        var result = customer.ChangeFirstName(newFirstName);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newFirstName, result.Value.FirstName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeFirstName_WithInvalidName_ReturnsFailureWithEmptyFirstNameError(string? newFirstName)
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);

        // Act
        var result = customer.ChangeFirstName(newFirstName!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyFirstName);
    }

    // ============================================================
    // ChangeLastName
    // ============================================================

    [TestMethod]
    public void ChangeLastName_WithValidName_ReturnsSuccessWithUpdatedLastName()
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);
        const string newLastName = "Smith";

        // Act
        var result = customer.ChangeLastName(newLastName);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newLastName, result.Value.LastName);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeLastName_WithInvalidName_ReturnsFailureWithEmptyLastNameError(string? newLastName)
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);

        // Act
        var result = customer.ChangeLastName(newLastName!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyLastName);
    }

    // ============================================================
    // ChangeEmail
    // ============================================================

    [TestMethod]
    public void ChangeEmail_WithValidEmail_ReturnsSuccessWithUpdatedEmail()
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);
        const string newEmail = "janet.smith@example.com";

        // Act
        var result = customer.ChangeEmail(newEmail);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newEmail, result.Value.Email);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeEmail_WithMissingEmail_ReturnsFailureWithEmptyEmailError(string? newEmail)
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);

        // Act
        var result = customer.ChangeEmail(newEmail!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyEmail);
    }

    [TestMethod]
    [DataRow("not-an-email")]
    [DataRow("missing-at-sign.com")]
    [DataRow("@no-local-part.com")]
    public void ChangeEmail_WithMalformedEmail_ReturnsFailureWithInvalidEmailError(string newEmail)
    {
        // Arrange
        var customer = Customer.Create(ValidFirstName, ValidLastName, ValidEmail).Value;
        Assert.IsNotNull(customer);

        // Act
        var result = customer.ChangeEmail(newEmail);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.InvalidEmail);
    }

    // ============================================================
    // Rehydrate
    // ============================================================

    [TestMethod]
    public void Rehydrate_WithGivenValues_ReturnsCustomerWithThoseExactValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var customer = Customer.Rehydrate(id, ValidFirstName, ValidLastName, ValidEmail);

        // Assert
        Assert.AreEqual(id, customer.Id);
        Assert.AreEqual(ValidFirstName, customer.FirstName);
        Assert.AreEqual(ValidLastName, customer.LastName);
        Assert.AreEqual(ValidEmail, customer.Email);
    }
}