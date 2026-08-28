using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests.Inventory;

public class StockQuantityTests
{
    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenValueIsNegative()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => new StockQuantity(-1));
    }

    [Fact]
    public void Add_ShouldIncreaseQuantity()
    {
        // Arrange
        var q1 = new StockQuantity(10);
        var q2 = new StockQuantity(5);

        // Act
        var result = q1 + q2;

        // Assert
        Assert.Equal(15, result.Value);
    }

    [Fact]
    public void Subtract_ShouldDecreaseQuantity()
    {
        // Arrange
        var q1 = new StockQuantity(10);
        var q2 = new StockQuantity(5);

        // Act
        var result = q1 - q2;

        // Assert
        Assert.Equal(5, result.Value);
    }

    [Fact]
    public void Subtract_ShouldThrowDomainException_WhenResultIsNegative()
    {
        // Arrange
        var q1 = new StockQuantity(5);
        var q2 = new StockQuantity(10);

        // Act & Assert
        Assert.Throws<DomainException>(() => q1 - q2);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenValuesMatch()
    {
        // Arrange
        var q1 = new StockQuantity(10);
        var q2 = new StockQuantity(10);

        // Act
        var result = q1 == q2;

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ComparisonOperators_ShouldWorkCorrectly()
    {
        // Arrange
        var q1 = new StockQuantity(10);
        var q2 = new StockQuantity(5);
        var q3 = new StockQuantity(10);

        // Assert
        Assert.True(q1 > q2);
        Assert.True(q2 < q1);
        Assert.True(q1 >= q3);
        Assert.True(q1 <= q3);
    }
}
