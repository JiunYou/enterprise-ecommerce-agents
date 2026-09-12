using EnterpriseCommerce.Domain.Catalog;
using FluentAssertions;

namespace EnterpriseCommerce.Domain.UnitTests.Catalog;

public class ProductTests
{
    [Fact]
    public void Create_WithValidData_ReturnsSuccess()
    {
        // Act
        var result = Product.Create("Test Product", "SKU-123", 100m, "TWD");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Product");
        result.Value.Sku.Should().Be("SKU-123");
        result.Value.Price.Should().Be(100m);
        result.Value.Currency.Should().Be("TWD");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithZeroOrNegativePrice_ReturnsFailure()
    {
        // Act
        var resultZero = Product.Create("Test Product", "SKU-123", 0m, "TWD");
        var resultNegative = Product.Create("Test Product", "SKU-123", -10m, "TWD");

        // Assert
        resultZero.IsFailure.Should().BeTrue();
        resultZero.Error.Should().Be(ProductErrors.InvalidPrice);
        
        resultNegative.IsFailure.Should().BeTrue();
        resultNegative.Error.Should().Be(ProductErrors.InvalidPrice);
    }

    [Fact]
    public void UpdatePrice_WithValidPrice_UpdatesPrice()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.UpdatePrice(150m);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Price.Should().Be(150m);
    }

    [Fact]
    public void UpdatePrice_WithZeroOrNegativePrice_ReturnsFailure()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;

        // Act
        var resultZero = product.UpdatePrice(0m);
        var resultNegative = product.UpdatePrice(-50m);

        // Assert
        resultZero.IsFailure.Should().BeTrue();
        resultZero.Error.Should().Be(ProductErrors.InvalidPrice);
        
        resultNegative.IsFailure.Should().BeTrue();
        resultNegative.Error.Should().Be(ProductErrors.InvalidPrice);
        
        product.Price.Should().Be(100m); // Price should remain unchanged
    }

    [Fact]
    public void Deactivate_WhenActive_SetsIsActiveToFalse()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.Deactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyDeactivated_ReturnsFailure()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        product.Deactivate();

        // Act
        var result = product.Deactivate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.AlreadyDeactivated);
    }

    [Fact]
    public void Reactivate_WhenInactive_SetsIsActiveToTrue()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        product.Deactivate();

        // Act
        var result = product.Reactivate();

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.IsActive.Should().BeTrue();
        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-123");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ReturnsFailure()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.Reactivate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.AlreadyActive);
        product.IsActive.Should().BeTrue();
        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-123");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.Rename("New Name");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("New Name");
    }

    [Fact]
    public void Rename_WithWhitespacePadding_NormalizesWithTrim()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.Rename("  New Name  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("New Name");
    }

    [Fact]
    public void Rename_WithEmptyOrWhitespace_ReturnsInvalidNameAndPreservesOldName()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-123", 100m, "TWD").Value;

        // Act
        var resultEmpty = product.Rename("");
        var resultWhitespace = product.Rename("   ");

        // Assert
        resultEmpty.IsFailure.Should().BeTrue();
        resultEmpty.Error.Should().Be(ProductErrors.InvalidName);
        product.Name.Should().Be("Old Name");

        resultWhitespace.IsFailure.Should().BeTrue();
        resultWhitespace.Error.Should().Be(ProductErrors.InvalidName);
        product.Name.Should().Be("Old Name");
    }

    [Fact]
    public void Rename_WithLengthExceeding255_ReturnsInvalidNameAndPreservesOldName()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-123", 100m, "TWD").Value;
        var longName = new string('A', 256);

        // Act
        var result = product.Rename(longName);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidName);
        product.Name.Should().Be("Old Name");
    }

    [Fact]
    public void Rename_SuccessfulRename_PreservesAllInvariants()
    {
        // Arrange
        var originalId = Guid.NewGuid();
        var product = Product.Create("Old Name", "SKU-123", 100m, "TWD").Value;
        var idBefore = product.Id;

        // Act
        var result = product.Rename("New Name");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Id.Should().Be(idBefore);
        product.Sku.Should().Be("SKU-123");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Rename_InactiveProduct_SuccessfullyRenamesWithoutChangingLifecycleStatus()
    {
        // Arrange
        var product = Product.Create("Old Name", "SKU-123", 100m, "TWD").Value;
        product.Deactivate();

        // Act
        var result = product.Rename("New Inactive Name");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("New Inactive Name");
        product.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Create_NewProduct_InitializesDescriptionToEmptyString()
    {
        // Act
        var result = Product.Create("Test Product", "SKU-123", 100m, "TWD");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().Be(string.Empty);
    }

    [Fact]
    public void UpdateDescription_WithValidDescription_SetsDescription()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.UpdateDescription("Product description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Description.Should().Be("Product description");
    }

    [Fact]
    public void UpdateDescription_WithSurroundingWhitespace_TrimsDescription()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;

        // Act
        var result = product.UpdateDescription("  Description  ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Description.Should().Be("Description");
    }

    [Fact]
    public void UpdateDescription_WithWhitespaceOrEmpty_ClearsDescriptionToEmptyString()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        product.UpdateDescription("Initial description");

        // Act & Assert for empty string
        var resultEmpty = product.UpdateDescription("");
        resultEmpty.IsSuccess.Should().BeTrue();
        product.Description.Should().Be(string.Empty);

        // Act & Assert for whitespace
        product.UpdateDescription("Another description");
        var resultWhitespace = product.UpdateDescription("   ");
        resultWhitespace.IsSuccess.Should().BeTrue();
        product.Description.Should().Be(string.Empty);
    }

    [Fact]
    public void UpdateDescription_WithNull_ReturnsInvalidDescriptionAndPreservesPrevious()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        product.UpdateDescription("Existing description");

        // Act
        var result = product.UpdateDescription(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidDescription);
        product.Description.Should().Be("Existing description");
    }

    [Fact]
    public void UpdateDescription_WithNormalizedLengthExceeding2000_ReturnsInvalidDescriptionAndPreservesPrevious()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        product.UpdateDescription("Existing description");
        var longDescription = new string('A', 2001);

        // Act
        var result = product.UpdateDescription(longDescription);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.InvalidDescription);
        product.Description.Should().Be("Existing description");
    }

    [Fact]
    public void UpdateDescription_SuccessfulUpdate_PreservesAllInvariants()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        var idBefore = product.Id;

        // Act
        var result = product.UpdateDescription("Valid new description");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Id.Should().Be(idBefore);
        product.Name.Should().Be("Test Product");
        product.Sku.Should().Be("SKU-123");
        product.Price.Should().Be(100m);
        product.Currency.Should().Be("TWD");
        product.IsActive.Should().BeTrue();
    }

    [Fact]
    public void UpdateDescription_InactiveProduct_SucceedsWithoutLifecycleChange()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-123", 100m, "TWD").Value;
        product.Deactivate();

        // Act
        var result = product.UpdateDescription("Description on inactive product");

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Description.Should().Be("Description on inactive product");
        product.IsActive.Should().BeFalse();
    }
}
