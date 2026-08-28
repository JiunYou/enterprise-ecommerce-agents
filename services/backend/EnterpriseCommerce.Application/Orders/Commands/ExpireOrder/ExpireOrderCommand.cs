using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.ExpireOrder;

public sealed record ExpireOrderCommand(Guid OrderId) : ICommand;
