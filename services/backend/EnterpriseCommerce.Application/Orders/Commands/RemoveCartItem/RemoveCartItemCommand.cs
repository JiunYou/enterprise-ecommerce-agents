using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.RemoveCartItem;

public sealed record RemoveCartItemCommand(
    Guid CustomerId,
    Guid ProductId) : ICommand;
