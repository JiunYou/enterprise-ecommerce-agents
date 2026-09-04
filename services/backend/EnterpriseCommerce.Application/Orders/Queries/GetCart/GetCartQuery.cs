using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Queries.GetCart;

public sealed record GetCartQuery(Guid CustomerId) : IQuery<CartResponse>;
