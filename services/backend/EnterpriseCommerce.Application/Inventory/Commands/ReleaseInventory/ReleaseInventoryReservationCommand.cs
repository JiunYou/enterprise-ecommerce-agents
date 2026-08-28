using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Inventory.Commands.ReleaseInventory;

public sealed record ReleaseInventoryReservationCommand(Guid ProductId, Guid OrderId) : ICommand;
