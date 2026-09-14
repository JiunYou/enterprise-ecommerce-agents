using EnterpriseCommerce.Domain.Customers;
using FluentAssertions;
using System;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Customers;

public class CustomerAddressTests
{
    private readonly Guid _validId = Guid.NewGuid();
    private readonly Guid _validCustomerId = Guid.NewGuid();
    private readonly DateTimeOffset _validCreatedAt = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_ShouldReturnSuccess()
    {
        // Act
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "tw",
            "100",
            "台北市",
            "中正區忠孝西路一段",
            "3 樓之 1",
            _validCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var address = result.Value;
        address.Id.Should().Be(_validId);
        address.CustomerId.Should().Be(_validCustomerId);
        address.RecipientName.Should().Be("王小明");
        address.Phone.Should().Be("0912345678");
        address.CountryCode.Should().Be("TW"); // Normalized to uppercase
        address.PostalCode.Should().Be("100");
        address.City.Should().Be("台北市");
        address.AddressLine1.Should().Be("中正區忠孝西路一段");
        address.AddressLine2.Should().Be("3 樓之 1");
        address.CreatedAt.Should().Be(_validCreatedAt);
    }

    [Fact]
    public void Create_WithEmptyId_ShouldReturnInvalidIdError()
    {
        var result = CustomerAddress.Create(
            Guid.Empty,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidId);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldReturnInvalidCustomerIdError()
    {
        var result = CustomerAddress.Create(
            _validId,
            Guid.Empty,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidCustomerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingRecipientName_ShouldReturnInvalidRecipientNameError(string? recipientName)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            recipientName,
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidRecipientName);
    }

    [Fact]
    public void Create_WithRecipientNameHavingSurroundingWhitespace_ShouldTrimRecipientName()
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "  王小明  ",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecipientName.Should().Be("王小明");
    }

    [Fact]
    public void Create_WithRecipientNameExceedingMaxLength_ShouldReturnInvalidRecipientNameError()
    {
        var longName = new string('A', 101);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            longName,
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidRecipientName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingPhone_ShouldReturnInvalidPhoneError(string? phone)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            phone,
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidPhone);
    }

    [Fact]
    public void Create_WithPhoneHavingSurroundingWhitespace_ShouldTrimPhone()
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "  0912345678  ",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Phone.Should().Be("0912345678");
    }

    [Fact]
    public void Create_WithPhoneExceedingMaxLength_ShouldReturnInvalidPhoneError()
    {
        var longPhone = new string('1', 31);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            longPhone,
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidPhone);
    }

    [Fact]
    public void Create_WithPhoneContainingControlCharacters_ShouldReturnInvalidPhoneError()
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912\n345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidPhone);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("T")]
    [InlineData("TWN")]
    public void Create_WithInvalidCountryCodeLength_ShouldReturnInvalidCountryCodeError(string? countryCode)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            countryCode,
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidCountryCode);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("T1")]
    [InlineData("T!")]
    [InlineData("台彎")]
    public void Create_WithNonAsciiLetterCountryCode_ShouldReturnInvalidCountryCodeError(string countryCode)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            countryCode,
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidCountryCode);
    }

    [Fact]
    public void Create_WithLowercaseCountryCode_ShouldNormalizeToUppercase()
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "us",
            "94105",
            "San Francisco",
            "Market St",
            null,
            _validCreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.CountryCode.Should().Be("US");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingPostalCode_ShouldReturnInvalidPostalCodeError(string? postalCode)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            postalCode,
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidPostalCode);
    }

    [Fact]
    public void Create_WithPostalCodeExceedingMaxLength_ShouldReturnInvalidPostalCodeError()
    {
        var longPostal = new string('1', 21);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            longPostal,
            "台北市",
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidPostalCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingCity_ShouldReturnInvalidCityError(string? city)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            city,
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidCity);
    }

    [Fact]
    public void Create_WithCityExceedingMaxLength_ShouldReturnInvalidCityError()
    {
        var longCity = new string('北', 101);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            longCity,
            "忠孝西路一段",
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidCity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingAddressLine1_ShouldReturnInvalidAddressLine1Error(string? addressLine1)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            addressLine1,
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidAddressLine1);
    }

    [Fact]
    public void Create_WithAddressLine1ExceedingMaxLength_ShouldReturnInvalidAddressLine1Error()
    {
        var longLine1 = new string('路', 201);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            longLine1,
            null,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidAddressLine1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithWhitespaceAddressLine2_ShouldNormalizeToNull(string? addressLine2)
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            addressLine2,
            _validCreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.AddressLine2.Should().BeNull();
    }

    [Fact]
    public void Create_WithAddressLine2HavingSurroundingWhitespace_ShouldTrimAddressLine2()
    {
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            "  3 樓之 1  ",
            _validCreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.AddressLine2.Should().Be("3 樓之 1");
    }

    [Fact]
    public void Create_WithAddressLine2ExceedingMaxLength_ShouldReturnInvalidAddressLine2Error()
    {
        var longLine2 = new string('樓', 201);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            longLine2,
            _validCreatedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CustomerAddressErrors.InvalidAddressLine2);
    }

    [Fact]
    public void Create_ShouldPreserveSuppliedCreatedAt()
    {
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var result = CustomerAddress.Create(
            _validId,
            _validCustomerId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            null,
            timestamp);

        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedAt.Should().Be(timestamp);
    }
}
