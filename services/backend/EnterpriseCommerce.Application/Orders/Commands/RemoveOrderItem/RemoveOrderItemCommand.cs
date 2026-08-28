using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem;

public sealed record RemoveOrderItemCommand(
    Guid OrderId,
    Guid ProductId) : ICommand;
