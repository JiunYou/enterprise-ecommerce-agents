using System;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlistItemStatus;

public sealed record WishlistItemStatusResponse(Guid ProductId, bool IsWishlisted);
