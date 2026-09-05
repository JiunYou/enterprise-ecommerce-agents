using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;

public sealed record GetAdminOrdersQuery(
    int Page = 1,
    int PageSize = 25,
    string? Status = null,
    Guid? OrderId = null) : IQuery<AdminOrderPageResponse>;
