namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderOperationsOverview;

public sealed record AdminOrderOperationsOverviewResponse(
    int TotalCount,
    int SubmittedCount,
    int PaidCount,
    int ShippedCount,
    int CancelledCount,
    IReadOnlyList<AdminOrderOperationsOverviewRecentOrder> RecentOrders);

public sealed record AdminOrderOperationsOverviewRecentOrder(
    Guid Id,
    string Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset SubmittedAt);
