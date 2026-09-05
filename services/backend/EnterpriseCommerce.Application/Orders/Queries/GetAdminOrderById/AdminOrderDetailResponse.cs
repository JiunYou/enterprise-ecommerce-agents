using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;

public sealed record AdminOrderDetailResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset? SubmittedAt,
    IReadOnlyCollection<OrderItemResponse> Items,
    ShippingAddressResponse? ShippingAddress = null);
