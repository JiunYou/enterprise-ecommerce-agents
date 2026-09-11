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

public sealed record ShipmentTrackingResponse(
    string Carrier,
    string TrackingNumber,
    DateTimeOffset ShippedAt);

public sealed record CustomerRefundStatusResponse(
    decimal Amount,
    string Currency,
    string Status,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? CompletedAt);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResponse> Items,
    ShippingAddressResponse? ShippingAddress = null,
    ShipmentTrackingResponse? ShipmentTracking = null,
    IReadOnlyList<CustomerRefundStatusResponse>? Refunds = null)
{
    public IReadOnlyList<CustomerRefundStatusResponse> Refunds { get; init; } = Refunds ?? Array.Empty<CustomerRefundStatusResponse>();
}
