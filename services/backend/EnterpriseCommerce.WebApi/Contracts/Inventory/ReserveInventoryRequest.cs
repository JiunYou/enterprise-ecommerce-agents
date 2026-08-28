namespace EnterpriseCommerce.WebApi.Contracts.Inventory;

public sealed record ReserveInventoryRequest(Guid ProductId, Guid OrderId, int Quantity);
