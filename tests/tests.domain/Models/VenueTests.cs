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
}