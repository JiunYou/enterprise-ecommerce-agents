using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlistItemStatus;

internal sealed class GetWishlistItemStatusQueryHandler : IQueryHandler<GetWishlistItemStatusQuery, WishlistItemStatusResponse>
{
    private readonly IWishlistRepository _wishlistRepository;

    public GetWishlistItemStatusQueryHandler(IWishlistRepository wishlistRepository)
    {
        _wishlistRepository = wishlistRepository;
    }

    public async Task<Result<WishlistItemStatusResponse>> Handle(GetWishlistItemStatusQuery request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result.Failure<WishlistItemStatusResponse>(WishlistErrors.InvalidCustomerId);
        }

        if (request.ProductId == Guid.Empty)
        {
            return Result.Failure<WishlistItemStatusResponse>(WishlistErrors.InvalidProductId);
        }

        var exists = await _wishlistRepository.ExistsAsync(
            request.CustomerId,
            request.ProductId,
            cancellationToken);

        return Result.Success(new WishlistItemStatusResponse(request.ProductId, exists));
    }
}
