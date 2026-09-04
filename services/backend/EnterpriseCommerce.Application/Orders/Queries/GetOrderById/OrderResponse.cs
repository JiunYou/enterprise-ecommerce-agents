namespace EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

public sealed record OrderItemResponse(
    Guid ProductId,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal TotalPrice);

public sealed record ShippingAddressResponse(
    string RecipientName,
    string Phone,
    string CountryCode,
    string PostalCode,
    string City,
    string AddressLine1,
    string? AddressLine2);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResponse> Items,
    ShippingAddressResponse? ShippingAddress = null);
