namespace EnterpriseCommerce.WebApi.Contracts.Orders;

public sealed record ShippingAddressRequest(
    string RecipientName,
    string Phone,
    string CountryCode,
    string PostalCode,
    string City,
    string AddressLine1,
    string? AddressLine2);

public sealed record SubmitOrderRequest(ShippingAddressRequest? ShippingAddress);
