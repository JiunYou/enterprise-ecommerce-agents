using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.UnitTests.Orders;

public class MoneyTests
{
    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenAmountIsNegative()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => new Money(-10, "USD"));
    }

    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenCurrencyIsEmpty()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => new Money(100, ""));
    }

    [Fact]
    public void Add_ShouldAddAmounts_WhenCurrenciesMatch()
    {
        // Arrange
        var m1 = new Money(100, "USD");
        var m2 = new Money(50, "USD");

        // Act
        var result = m1 + m2;

        // Assert
        Assert.Equal(150, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Add_ShouldThrowDomainException_WhenCurrenciesDoNotMatch()
    {
        // Arrange
        var m1 = new Money(100, "USD");
        var m2 = new Money(50, "EUR");

        // Act & Assert
        Assert.Throws<DomainException>(() => m1 + m2);
    }

    [Fact]
    public void Multiply_ShouldMultiplyAmount()
    {
        // Arrange
        var money = new Money(100, "USD");

        // Act
        var result = money * 3;

        // Assert
        Assert.Equal(300, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Multiply_ShouldThrowDomainException_WhenMultiplierIsNegative()
    {
        // Arrange
        var money = new Money(100, "USD");

        // Act & Assert
        Assert.Throws<DomainException>(() => money * -1);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenAmountAndCurrencyMatch()
    {
        // Arrange
        var m1 = new Money(100, "USD");
        var m2 = new Money(100, "USD");

        // Act
        var result = m1 == m2;

        // Assert
        Assert.True(result);
    }
}
