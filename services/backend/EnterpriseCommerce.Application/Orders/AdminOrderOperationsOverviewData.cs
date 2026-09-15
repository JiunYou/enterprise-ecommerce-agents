namespace EnterpriseCommerce.Application.Orders;

public sealed record AdminOrderOperationsOverviewData(
    int TotalCount,
    int SubmittedCount,
    int PaidCount,
    int ShippedCount,
    int CancelledCount,
    IReadOnlyList<AdminOrderOverviewRecentOrderData> RecentOrders);

public sealed record AdminOrderOverviewRecentOrderData(
    Guid Id,
    string Status,
    string Currency,
    decimal TotalAmount,
    DateTimeOffset SubmittedAt);
