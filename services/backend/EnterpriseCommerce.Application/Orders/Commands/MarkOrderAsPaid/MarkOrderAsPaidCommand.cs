using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid;

public sealed record MarkOrderAsPaidCommand(Guid OrderId) : ICommand;
