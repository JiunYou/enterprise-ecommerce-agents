using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlist;

public sealed record GetWishlistQuery(
    Guid CustomerId,
    int Page = 1,
    int PageSize = 25) : IQuery<WishlistResponse>;
