using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.ShipOrder;

public sealed record ShipOrderCommand(Guid OrderId, string Carrier, string TrackingNumber) : ICommand;
