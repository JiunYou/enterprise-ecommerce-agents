namespace EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;

public sealed record ShippingAddressDto(
    string RecipientName,
    string Phone,
    string CountryCode,
    string PostalCode,
    string City,
    string AddressLine1,
    string? AddressLine2);
