using EnterpriseCommerce.Domain.Marketing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Wishlist;

public interface IWishlistRepository
{
    Task<WishlistItem?> GetByCustomerAndProductAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<WishlistItem> Items, int TotalCount)> GetPagedByCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default);

    void Add(WishlistItem item);

    void Remove(WishlistItem item);
}
