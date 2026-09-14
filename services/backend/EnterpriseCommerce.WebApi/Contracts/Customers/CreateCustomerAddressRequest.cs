namespace EnterpriseCommerce.WebApi.Contracts.Customers;

public sealed record CreateCustomerAddressRequest(
    string RecipientName,
    string Phone,
    string CountryCode,
    string PostalCode,
    string City,
    string AddressLine1,
    string? AddressLine2);
