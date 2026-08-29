using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.CancelOrder;

public sealed record CancelOrderCommand(Guid OrderId, Guid CustomerId) : ICommand;
