namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;

public sealed record AdminOrderSummaryResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset? SubmittedAt);
