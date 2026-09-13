using EnterpriseCommerce.Application.Marketing.Wishlist;
using EnterpriseCommerce.Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Infrastructure.Persistence.Marketing;

internal sealed class WishlistRepository : IWishlistRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public WishlistRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WishlistItem?> GetByCustomerAndProductAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.WishlistItems
            .FirstOrDefaultAsync(
                w => w.CustomerId == customerId && w.ProductId == productId,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<WishlistItem> Items, int TotalCount)> GetPagedByCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.WishlistItems
            .AsNoTracking()
            .Where(w => w.CustomerId == customerId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(w => w.AddedAt)
            .ThenByDescending(w => w.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(WishlistItem item)
    {
        _dbContext.WishlistItems.Add(item);
    }

    public void Remove(WishlistItem item)
    {
        _dbContext.WishlistItems.Remove(item);
    }
}
