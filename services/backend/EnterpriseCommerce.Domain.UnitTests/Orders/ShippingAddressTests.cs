using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Orders;

public class ShippingAddressTests
{
    [Fact]
    public void Create_ShouldSucceed_WhenAllFieldsAreValid()
    {
        // Act
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "tw",
            "100",
            "Taipei",
            "Section 1, Zhongxiao E. Rd.",
            "Room 101");

        // Assert
        Assert.True(result.IsSuccess);
        var address = result.Value;
        Assert.Equal("Jane Doe", address.RecipientName);
        Assert.Equal("0912345678", address.Phone);
        Assert.Equal("TW", address.CountryCode); // Normalized to uppercase
        Assert.Equal("100", address.PostalCode);
        Assert.Equal("Taipei", address.City);
        Assert.Equal("Section 1, Zhongxiao E. Rd.", address.AddressLine1);
        Assert.Equal("Room 101", address.AddressLine2);
    }

    [Fact]
    public void Create_ShouldSucceed_WhenAddressLine2IsNull()
    {
        // Act
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "US",
            "94105",
            "San Francisco",
            "Market St",
            null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AddressLine2);
    }

    [Fact]
    public void Create_ShouldNormalizeWhitespace_ForAddressLine2ToNull()
    {
        // Act
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "US",
            "94105",
            "San Francisco",
            "Market St",
            "   ");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AddressLine2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenRecipientNameIsMissing(string? recipientName)
    {
        var result = ShippingAddress.Create(
            recipientName,
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingRecipientName, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenRecipientNameExceedsMaxLength()
    {
        var longName = new string('A', ShippingAddress.MaxRecipientNameLength + 1);
        var result = ShippingAddress.Create(
            longName,
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingRecipientName, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenPhoneIsMissing(string? phone)
    {
        var result = ShippingAddress.Create(
            "Jane Doe",
            phone,
            "TW",
            "100",
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingPhone, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenPhoneContainsControlCharacters()
    {
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912\n345678",
            "TW",
            "100",
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingPhone, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenPhoneExceedsMaxLength()
    {
        var longPhone = new string('1', ShippingAddress.MaxPhoneLength + 1);
        var result = ShippingAddress.Create(
            "Jane Doe",
            longPhone,
            "TW",
            "100",
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingPhone, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("T")]
    [InlineData("TWN")]
    [InlineData("T1")]
    [InlineData("12")]
    public void Create_ShouldFail_WhenCountryCodeIsInvalid(string? countryCode)
    {
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            countryCode,
            "100",
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingCountryCode, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenPostalCodeIsMissing(string? postalCode)
    {
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            postalCode,
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingPostalCode, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenPostalCodeExceedsMaxLength()
    {
        var longPostal = new string('1', ShippingAddress.MaxPostalCodeLength + 1);
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            longPostal,
            "Taipei",
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingPostalCode, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenCityIsMissing(string? city)
    {
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            city,
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingCity, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenCityExceedsMaxLength()
    {
        var longCity = new string('A', ShippingAddress.MaxCityLength + 1);
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            longCity,
            "Address Line 1");

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingCity, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldFail_WhenAddressLine1IsMissing(string? addressLine1)
    {
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            addressLine1);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingAddressLine1, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenAddressLine1ExceedsMaxLength()
    {
        var longLine1 = new string('A', ShippingAddress.MaxAddressLineLength + 1);
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            longLine1);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingAddressLine1, result.Error);
    }

    [Fact]
    public void Create_ShouldFail_WhenAddressLine2ExceedsMaxLength()
    {
        var longLine2 = new string('A', ShippingAddress.MaxAddressLineLength + 1);
        var result = ShippingAddress.Create(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "Address Line 1",
            longLine2);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidShippingAddressLine2, result.Error);
    }

    [Fact]
    public void ValueObjectEquality_ShouldBeEqual_WhenAllComponentsMatch()
    {
        var address1 = ShippingAddress.Create("Jane Doe", "0912345678", "TW", "100", "Taipei", "Line 1", "Line 2").Value;
        var address2 = ShippingAddress.Create("Jane Doe", "0912345678", "tw", "100", "Taipei", "Line 1", "Line 2").Value;

        Assert.Equal(address1, address2);
        Assert.True(address1 == address2);
    }
}
