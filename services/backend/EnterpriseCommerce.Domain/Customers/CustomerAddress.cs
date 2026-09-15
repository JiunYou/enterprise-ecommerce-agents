using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Linq;

namespace EnterpriseCommerce.Domain.Customers;

public sealed class CustomerAddress : Entity<Guid>
{
    public const int MaxRecipientNameLength = 100;
    public const int MaxPhoneLength = 30;
    public const int CountryCodeLength = 2;
    public const int MaxPostalCodeLength = 20;
    public const int MaxCityLength = 100;
    public const int MaxAddressLineLength = 200;

    public Guid CustomerId { get; private set; }
    public string RecipientName { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string CountryCode { get; private set; } = default!;
    public string PostalCode { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string AddressLine1 { get; private set; } = default!;
    public string? AddressLine2 { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsDefault { get; private set; }

    private CustomerAddress(
        Guid id,
        Guid customerId,
        string recipientName,
        string phone,
        string countryCode,
        string postalCode,
        string city,
        string addressLine1,
        string? addressLine2,
        DateTimeOffset createdAt)
        : base(id)
    {
        CustomerId = customerId;
        RecipientName = recipientName;
        Phone = phone;
        CountryCode = countryCode;
        PostalCode = postalCode;
        City = city;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
        CreatedAt = createdAt;
        IsDefault = false;
    }

    private CustomerAddress() : base(Guid.Empty)
    {
        IsDefault = false;
    }

    public static Result<CustomerAddress> Create(
        Guid id,
        Guid customerId,
        string? recipientName,
        string? phone,
        string? countryCode,
        string? postalCode,
        string? city,
        string? addressLine1,
        string? addressLine2,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<CustomerAddress>(CustomerAddressErrors.InvalidId);
        }

        if (customerId == Guid.Empty)
        {
            return Result.Failure<CustomerAddress>(CustomerAddressErrors.InvalidCustomerId);
        }

        var validationResult = ValidateAndNormalize(
            recipientName,
            phone,
            countryCode,
            postalCode,
            city,
            addressLine1,
            addressLine2);

        if (validationResult.IsFailure)
        {
            return Result.Failure<CustomerAddress>(validationResult.Error);
        }

        var values = validationResult.Value;

        return Result.Success(new CustomerAddress(
            id,
            customerId,
            values.RecipientName,
            values.Phone,
            values.CountryCode,
            values.PostalCode,
            values.City,
            values.AddressLine1,
            values.AddressLine2,
            createdAt));
    }

    public Result Update(
        string? recipientName,
        string? phone,
        string? countryCode,
        string? postalCode,
        string? city,
        string? addressLine1,
        string? addressLine2)
    {
        var validationResult = ValidateAndNormalize(
            recipientName,
            phone,
            countryCode,
            postalCode,
            city,
            addressLine1,
            addressLine2);

        if (validationResult.IsFailure)
        {
            return Result.Failure(validationResult.Error);
        }

        var values = validationResult.Value;

        RecipientName = values.RecipientName;
        Phone = values.Phone;
        CountryCode = values.CountryCode;
        PostalCode = values.PostalCode;
        City = values.City;
        AddressLine1 = values.AddressLine1;
        AddressLine2 = values.AddressLine2;

        return Result.Success();
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void ClearDefault()
    {
        IsDefault = false;
    }

    private static Result<(
        string RecipientName,
        string Phone,
        string CountryCode,
        string PostalCode,
        string City,
        string AddressLine1,
        string? AddressLine2)> ValidateAndNormalize(
        string? recipientName,
        string? phone,
        string? countryCode,
        string? postalCode,
        string? city,
        string? addressLine1,
        string? addressLine2)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidRecipientName);
        }

        var trimmedRecipientName = recipientName.Trim();
        if (trimmedRecipientName.Length > MaxRecipientNameLength)
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidRecipientName);
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidPhone);
        }

        var trimmedPhone = phone.Trim();
        if (trimmedPhone.Length > MaxPhoneLength || trimmedPhone.Any(char.IsControl))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidPhone);
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidCountryCode);
        }

        var trimmedCountryCode = countryCode.Trim();
        if (trimmedCountryCode.Length != CountryCodeLength || !trimmedCountryCode.All(char.IsAsciiLetter))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidCountryCode);
        }

        var normalizedCountryCode = trimmedCountryCode.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidPostalCode);
        }

        var trimmedPostalCode = postalCode.Trim();
        if (trimmedPostalCode.Length > MaxPostalCodeLength)
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidPostalCode);
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidCity);
        }

        var trimmedCity = city.Trim();
        if (trimmedCity.Length > MaxCityLength)
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidCity);
        }

        if (string.IsNullOrWhiteSpace(addressLine1))
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidAddressLine1);
        }

        var trimmedAddressLine1 = addressLine1.Trim();
        if (trimmedAddressLine1.Length > MaxAddressLineLength)
        {
            return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidAddressLine1);
        }

        string? trimmedAddressLine2 = null;
        if (!string.IsNullOrWhiteSpace(addressLine2))
        {
            trimmedAddressLine2 = addressLine2.Trim();
            if (trimmedAddressLine2.Length > MaxAddressLineLength)
            {
                return Result.Failure<(string, string, string, string, string, string, string?)>(CustomerAddressErrors.InvalidAddressLine2);
            }
        }

        return Result.Success((
            trimmedRecipientName,
            trimmedPhone,
            normalizedCountryCode,
            trimmedPostalCode,
            trimmedCity,
            trimmedAddressLine1,
            trimmedAddressLine2));
    }
}
