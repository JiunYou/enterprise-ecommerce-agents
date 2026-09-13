using System;
using System.Collections.Generic;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlist;

public sealed record WishlistItemResponse(Guid ProductId, DateTimeOffset AddedAt);

public sealed record WishlistResponse(
    IReadOnlyList<WishlistItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
