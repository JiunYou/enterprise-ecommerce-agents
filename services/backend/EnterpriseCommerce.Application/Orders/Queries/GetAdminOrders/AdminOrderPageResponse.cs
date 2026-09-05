namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;

public sealed record AdminOrderPageResponse(
    IReadOnlyList<AdminOrderSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
