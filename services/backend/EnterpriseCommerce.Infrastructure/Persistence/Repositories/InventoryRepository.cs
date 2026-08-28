using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class InventoryRepository : IInventoryRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public InventoryRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<InventoryItem?> GetByProductIdAsync(ProductReference productReference, CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryItems
            .Include(i => i.Reservations)
            .FirstOrDefaultAsync(i => i.ProductReference == productReference, cancellationToken);
    }

    public async Task<InventoryItem?> GetByProductIdForUpdateAsync(ProductReference productReference, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.InventoryItems
            .FromSqlRaw("SELECT * FROM InventoryItems WHERE ProductReference = {0} FOR UPDATE", productReference.Value)
            .FirstOrDefaultAsync(cancellationToken);
            
        if (item != null)
        {
            await _dbContext.Entry(item).Collection(i => i.Reservations).LoadAsync(cancellationToken);
        }
        
        return item;
    }
}
