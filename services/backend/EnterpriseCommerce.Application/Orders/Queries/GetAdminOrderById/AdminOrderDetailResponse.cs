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
    ShippingAddressResponse? ShippingAddress = null,
    AdminCancellationResponse? AdminCancellation = null);

public sealed record AdminCancellationResponse(
    string ActorIssuer,
    string ActorSubject,
    DateTimeOffset CancelledAt,
    string Reason);
