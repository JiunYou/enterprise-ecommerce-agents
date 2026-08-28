using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

public sealed record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderResponse>;
