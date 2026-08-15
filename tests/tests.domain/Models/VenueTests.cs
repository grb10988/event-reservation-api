using EventReservation.Domain.Models;

namespace EventReservation.Tests.Domain.Models;

[TestClass]
public class VenueTests
{
    private const string ValidName = "City Amphitheater";
    private const string ValidAddress = "123 Main St, Springfield";
    private const int ValidCapacity = 5000;

    // ============================================================
    // Create
    // ============================================================

    [TestMethod]
    public void Create_WithValidInputs_ReturnsSuccessWithExpectedValues()
    {
        // Act
        var result = Venue.Create(ValidName, ValidAddress, ValidCapacity);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(ValidName, result.Value.Name);
        Assert.AreEqual(ValidAddress, result.Value.Address);
        Assert.AreEqual(ValidCapacity, result.Value.Capacity);
        Assert.AreNotEqual(Guid.Empty, result.Value.Id);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidName_ReturnsFailureWithEmptyNameError(string? name)
    {
        // Act
        var result = Venue.Create(name!, ValidAddress, ValidCapacity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyName);
    }

    [TestMethod]
    public void Create_WithNameOverMaxLength_ReturnsFailureWithNameOutOfRangeError()
    {
        // Arrange
        var tooLongName = new string('A', 151);

        // Act
        var result = Venue.Create(tooLongName, ValidAddress, ValidCapacity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.NameOutOfRange);
    }

    [TestMethod]
    public void Create_WithNameAtMaxLength_ReturnsSuccess()
    {
        // Arrange - boundary check: exactly 150 characters should be allowed
        var maxLengthName = new string('A', 150);

        // Act
        var result = Venue.Create(maxLengthName, ValidAddress, ValidCapacity);

        // Assert
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Create_WithInvalidAddress_ReturnsFailureWithEmptyAddressError(string? address)
    {
        // Act
        var result = Venue.Create(ValidName, address!, ValidCapacity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyAddress);
    }

    [TestMethod]
    public void Create_WithAddressOverMaxLength_ReturnsFailureWithAddressOutOfRangeError()
    {
        // Arrange
        var tooLongAddress = new string('A', 301);

        // Act
        var result = Venue.Create(ValidName, tooLongAddress, ValidCapacity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.AddressOutOfRange);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Create_WithInvalidCapacity_ReturnsFailureWithInvalidCapacityError(int capacity)
    {
        // Act
        var result = Venue.Create(ValidName, ValidAddress, capacity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.InvalidCapacity);
    }

    [TestMethod]
    public void Create_WithAllInvalidInputs_AccumulatesAllErrors()
    {
        // Act
        var result = Venue.Create("", "", 0);

        // Assert
        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(3, result.Errors.Count);
    }

    [TestMethod]
    public void Create_CalledTwiceWithSameInputs_GeneratesDifferentIds()
    {
        // Act
        var first = Venue.Create(ValidName, ValidAddress, ValidCapacity);
        var second = Venue.Create(ValidName, ValidAddress, ValidCapacity);

        // Assert
        Assert.IsTrue(first.IsSuccess);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreNotEqual(first.Value.Id, second.Value.Id);
    }

    // ============================================================
    // ChangeName
    // ============================================================

    [TestMethod]
    public void ChangeName_WithValidName_ReturnsSuccessWithUpdatedName()
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);
        const string newName = "Riverside Arena";

        // Act
        var result = venue.ChangeName(newName);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newName, result.Value.Name);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeName_WithInvalidName_ReturnsFailureWithEmptyNameError(string? newName)
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);

        // Act
        var result = venue.ChangeName(newName!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyName);
    }

    [TestMethod]
    public void ChangeName_WithNameOverMaxLength_ReturnsFailureWithNameOutOfRangeError()
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);
        var tooLongName = new string('A', 151);

        // Act
        var result = venue.ChangeName(tooLongName);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.NameOutOfRange);
    }

    // ============================================================
    // ChangeAddress
    // ============================================================

    [TestMethod]
    public void ChangeAddress_WithValidAddress_ReturnsSuccessWithUpdatedAddress()
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);
        const string newAddress = "456 Oak Ave, Shelbyville";

        // Act
        var result = venue.ChangeAddress(newAddress);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newAddress, result.Value.Address);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ChangeAddress_WithInvalidAddress_ReturnsFailureWithEmptyAddressError(string? newAddress)
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);

        // Act
        var result = venue.ChangeAddress(newAddress!);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.EmptyAddress);
    }

    [TestMethod]
    public void ChangeAddress_WithAddressOverMaxLength_ReturnsFailureWithAddressOutOfRangeError()
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);
        var tooLongAddress = new string('A', 301);

        // Act
        var result = venue.ChangeAddress(tooLongAddress);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.AddressOutOfRange);
    }

    // ============================================================
    // ChangeCapacity
    // ============================================================

    [TestMethod]
    public void ChangeCapacity_WithValidCapacity_ReturnsSuccessWithUpdatedCapacity()
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);
        const int newCapacity = 7500;

        // Act
        var result = venue.ChangeCapacity(newCapacity);

        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(newCapacity, result.Value.Capacity);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void ChangeCapacity_WithInvalidCapacity_ReturnsFailureWithInvalidCapacityError(int newCapacity)
    {
        // Arrange
        var venue = Venue.Create(ValidName, ValidAddress, ValidCapacity).Value;
        Assert.IsNotNull(venue);

        // Act
        var result = venue.ChangeCapacity(newCapacity);

        // Assert
        Assert.IsTrue(result.IsFailure);
        CollectionAssert.Contains(result.Errors.ToList(), Venue.Errors.InvalidCapacity);
    }

    // ============================================================
    // Rehydrate
    // ============================================================

    [TestMethod]
    public void Rehydrate_WithGivenValues_ReturnsVenueWithThoseExactValues()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var venue = Venue.Rehydrate(id, ValidName, ValidAddress, ValidCapacity);

        // Assert
        Assert.AreEqual(id, venue.Id);
        Assert.AreEqual(ValidName, venue.Name);
        Assert.AreEqual(ValidAddress, venue.Address);
        Assert.AreEqual(ValidCapacity, venue.Capacity);
    }
}