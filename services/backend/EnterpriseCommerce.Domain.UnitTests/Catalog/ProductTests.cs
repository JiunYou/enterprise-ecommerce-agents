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
}
