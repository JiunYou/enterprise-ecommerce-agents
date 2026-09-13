using EnterpriseCommerce.Domain.Primitives;
using System;

namespace EnterpriseCommerce.Domain.Marketing;

public sealed class WishlistItem : Entity<Guid>
{
    private WishlistItem(Guid id, Guid customerId, Guid productId, DateTimeOffset addedAt)
        : base(id)
    {
        CustomerId = customerId;
        ProductId = productId;
        AddedAt = addedAt;
    }

    private WishlistItem()
    {
    }

    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    public static Result<WishlistItem> Create(Guid id, Guid customerId, Guid productId, DateTimeOffset addedAt)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<WishlistItem>(WishlistErrors.InvalidId);
        }

        if (customerId == Guid.Empty)
        {
            return Result.Failure<WishlistItem>(WishlistErrors.InvalidCustomerId);
        }

        if (productId == Guid.Empty)
        {
            return Result.Failure<WishlistItem>(WishlistErrors.InvalidProductId);
        }

        return Result.Success(new WishlistItem(id, customerId, productId, addedAt));
    }
}
