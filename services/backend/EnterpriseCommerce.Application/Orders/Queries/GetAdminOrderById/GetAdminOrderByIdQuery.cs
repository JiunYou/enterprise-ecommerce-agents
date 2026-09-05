using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;

public sealed record GetAdminOrderByIdQuery(Guid OrderId) : IQuery<AdminOrderDetailResponse>;
