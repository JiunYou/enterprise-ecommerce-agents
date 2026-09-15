using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderOperationsOverview;

public sealed record GetAdminOrderOperationsOverviewQuery : IQuery<AdminOrderOperationsOverviewResponse>;
