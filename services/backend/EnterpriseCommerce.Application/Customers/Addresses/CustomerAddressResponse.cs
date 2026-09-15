using System;

namespace EnterpriseCommerce.Application.Customers.Addresses;

public sealed record CustomerAddressResponse(
    Guid Id,
    string RecipientName,
    string Phone,
    string CountryCode,
    string PostalCode,
    string City,
    string AddressLine1,
    string? AddressLine2,
    bool IsDefault);
