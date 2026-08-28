using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;

namespace EnterpriseCommerce.Application.Inventory;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByProductIdAsync(ProductReference productId, CancellationToken cancellationToken = default);
    Task<InventoryItem?> GetByProductIdForUpdateAsync(ProductReference productId, CancellationToken cancellationToken = default);
}
