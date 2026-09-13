using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlist;

internal sealed class GetWishlistQueryHandler : IQueryHandler<GetWishlistQuery, WishlistResponse>
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly IWishlistRepository _wishlistRepository;

    public GetWishlistQueryHandler(IWishlistRepository wishlistRepository)
    {
        _wishlistRepository = wishlistRepository;
    }

    public async Task<Result<WishlistResponse>> Handle(GetWishlistQuery request, CancellationToken cancellationToken)
    {
        var normalizedPage = request.Page <= 0 ? DefaultPage : request.Page;
        var normalizedPageSize = request.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(request.PageSize, MaxPageSize);

        var (items, totalCount) = await _wishlistRepository.GetPagedByCustomerAsync(
            request.CustomerId,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        var responseItems = items.Select(item => new WishlistItemResponse(
            item.ProductId,
            item.AddedAt)).ToList();

        var response = new WishlistResponse(
            responseItems,
            normalizedPage,
            normalizedPageSize,
            totalCount);

        return Result.Success(response);
    }
}
