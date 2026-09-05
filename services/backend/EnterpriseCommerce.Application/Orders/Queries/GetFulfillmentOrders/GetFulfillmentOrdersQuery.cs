using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

namespace EnterpriseCommerce.Application.Orders.Queries.GetFulfillmentOrders;

public sealed record GetFulfillmentOrdersQuery(int Limit = 50) : IQuery<IReadOnlyList<OrderResponse>>;
