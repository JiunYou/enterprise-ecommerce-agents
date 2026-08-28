using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.CreateOrder;

public sealed record CreateOrderCommand(Guid CustomerId, string Currency) : ICommand<Guid>;
