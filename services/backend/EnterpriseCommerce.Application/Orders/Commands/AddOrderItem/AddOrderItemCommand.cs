using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.AddOrderItem;

public sealed record AddOrderItemCommand(
    Guid OrderId,
    Guid ProductId,
    int Quantity) : ICommand;
