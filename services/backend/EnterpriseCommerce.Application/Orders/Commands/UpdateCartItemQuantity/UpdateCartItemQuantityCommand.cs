using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.UpdateCartItemQuantity;

public sealed record UpdateCartItemQuantityCommand(
    Guid CustomerId,
    Guid ProductId,
    int Quantity) : ICommand;
