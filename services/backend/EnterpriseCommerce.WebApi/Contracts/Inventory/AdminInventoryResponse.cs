namespace EnterpriseCommerce.WebApi.Contracts.Inventory;

public sealed record AdminInventoryResponse(Guid ProductId, int AvailableQuantity, int ReservedQuantity);
