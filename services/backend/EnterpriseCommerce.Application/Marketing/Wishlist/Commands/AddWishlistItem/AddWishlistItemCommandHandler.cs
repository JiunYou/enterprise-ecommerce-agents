using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Commands.AddWishlistItem;

internal sealed class AddWishlistItemCommandHandler : ICommandHandler<AddWishlistItemCommand>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AddWishlistItemCommandHandler(
        IWishlistRepository wishlistRepository,
        IProductRepository productRepository,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _wishlistRepository = wishlistRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result> Handle(AddWishlistItemCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result.Failure(WishlistErrors.InvalidCustomerId);
        }

        if (request.ProductId == Guid.Empty)
        {
            return Result.Failure(WishlistErrors.InvalidProductId);
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);

        if (product is null || !product.IsActive)
        {
            // 不公開下架狀態，統一回傳 NotFound
            return Result.Failure(ProductErrors.NotFound);
        }

        var existingItem = await _wishlistRepository.GetByCustomerAndProductAsync(
            request.CustomerId,
            request.ProductId,
            cancellationToken);

        if (existingItem is not null)
        {
            return Result.Failure(WishlistErrors.AlreadyExists);
        }

        var addedAt = _timeProvider.GetUtcNow();
        var wishlistItemResult = WishlistItem.Create(Guid.NewGuid(), request.CustomerId, request.ProductId, addedAt);

        if (wishlistItemResult.IsFailure)
        {
            return Result.Failure(wishlistItemResult.Error);
        }

        _wishlistRepository.Add(wishlistItemResult.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
