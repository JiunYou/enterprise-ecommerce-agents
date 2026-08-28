using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Inventory.Commands.ReserveInventory;

public sealed record ReserveInventoryCommand(Guid ProductId, Guid OrderId, int Quantity) : ICommand;
