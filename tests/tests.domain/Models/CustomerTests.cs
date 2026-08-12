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
    public void Create_WithMissingFirstNameAndMalformedEmail_OnlyReturnsPresenceError()
    {
        // Act
        var result = Customer.Create("", ValidLastName, "not-an-email");

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Customer.Errors.EmptyFirstName);
        CollectionAssert.DoesNotContain(result.Errors.ToList(), Customer.Errors.InvalidEmail);
    }

    [TestMethod]
    public void Create_WithAllFieldsMissing_AccumulatesOnlyPresenceErrors()
    {
        // Act
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
}