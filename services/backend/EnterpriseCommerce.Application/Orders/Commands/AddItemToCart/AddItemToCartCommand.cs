using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;

namespace EnterpriseCommerce.Application.Orders.Commands.AddItemToCart;

public sealed record AddItemToCartCommand(
    Guid CustomerId,
    Guid ProductId,
    int Quantity) : ICommand<CartResponse>;
