using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders.ValueObjects;

public sealed class ShippingAddress : ValueObject
{
    public const int MaxRecipientNameLength = 100;
    public const int MaxPhoneLength = 30;
    public const int CountryCodeLength = 2;
    public const int MaxPostalCodeLength = 20;
    public const int MaxCityLength = 100;
    public const int MaxAddressLineLength = 200;

    public string RecipientName { get; private set; } = default!;
    public string Phone { get; private set; } = default!;
    public string CountryCode { get; private set; } = default!;
    public string PostalCode { get; private set; } = default!;
    public string City { get; private set; } = default!;
    public string AddressLine1 { get; private set; } = default!;
    public string? AddressLine2 { get; private set; }

    private ShippingAddress(
        string recipientName,
        string phone,
        string countryCode,
        string postalCode,
        string city,
        string addressLine1,
        string? addressLine2)
    {
        RecipientName = recipientName;
        Phone = phone;
        CountryCode = countryCode;
        PostalCode = postalCode;
        City = city;
        AddressLine1 = addressLine1;
        AddressLine2 = addressLine2;
    }

    private ShippingAddress()
    {
    }

    public static Result<ShippingAddress> Create(
        string? recipientName,
        string? phone,
        string? countryCode,
        string? postalCode,
        string? city,
        string? addressLine1,
        string? addressLine2 = null)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingRecipientName);
        }

        var trimmedRecipientName = recipientName.Trim();
        if (trimmedRecipientName.Length > MaxRecipientNameLength)
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingRecipientName);
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingPhone);
        }

        var trimmedPhone = phone.Trim();
        if (trimmedPhone.Length > MaxPhoneLength || trimmedPhone.Any(char.IsControl))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingPhone);
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingCountryCode);
        }

        var trimmedCountryCode = countryCode.Trim();
        if (trimmedCountryCode.Length != CountryCodeLength || !trimmedCountryCode.All(char.IsAsciiLetter))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingCountryCode);
        }

        var normalizedCountryCode = trimmedCountryCode.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingPostalCode);
        }

        var trimmedPostalCode = postalCode.Trim();
        if (trimmedPostalCode.Length > MaxPostalCodeLength)
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingPostalCode);
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingCity);
        }

        var trimmedCity = city.Trim();
        if (trimmedCity.Length > MaxCityLength)
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingCity);
        }

        if (string.IsNullOrWhiteSpace(addressLine1))
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingAddressLine1);
        }

        var trimmedAddressLine1 = addressLine1.Trim();
        if (trimmedAddressLine1.Length > MaxAddressLineLength)
        {
            return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingAddressLine1);
        }

        string? trimmedAddressLine2 = null;
        if (!string.IsNullOrWhiteSpace(addressLine2))
        {
            trimmedAddressLine2 = addressLine2.Trim();
            if (trimmedAddressLine2.Length > MaxAddressLineLength)
            {
                return Result.Failure<ShippingAddress>(OrderErrors.InvalidShippingAddressLine2);
            }
        }

        return Result.Success(new ShippingAddress(
            trimmedRecipientName,
            trimmedPhone,
            normalizedCountryCode,
            trimmedPostalCode,
            trimmedCity,
            trimmedAddressLine1,
            trimmedAddressLine2));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RecipientName;
        yield return Phone;
        yield return CountryCode;
        yield return PostalCode;
        yield return City;
        yield return AddressLine1;
        if (AddressLine2 is not null)
        {
            yield return AddressLine2;
        }
    }
}
