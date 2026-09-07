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
    AdminCancellationResponse? AdminCancellation = null,
    IReadOnlyCollection<AdminRefundRequiredPaymentResponse>? RefundRequiredPayments = null)
{
    public IReadOnlyCollection<AdminRefundRequiredPaymentResponse> RefundRequiredPayments { get; init; } = RefundRequiredPayments ?? Array.Empty<AdminRefundRequiredPaymentResponse>();
}

public sealed record AdminCancellationResponse(
    string ActorIssuer,
    string ActorSubject,
    DateTimeOffset CancelledAt,
    string Reason);
