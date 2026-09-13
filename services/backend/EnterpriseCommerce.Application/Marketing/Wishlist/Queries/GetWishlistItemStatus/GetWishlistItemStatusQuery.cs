using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlistItemStatus;

public sealed record GetWishlistItemStatusQuery(
    Guid CustomerId,
    Guid ProductId) : IQuery<WishlistItemStatusResponse>;
