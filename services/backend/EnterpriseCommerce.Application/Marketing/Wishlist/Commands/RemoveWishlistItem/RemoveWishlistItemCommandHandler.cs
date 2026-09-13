using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Wishlist.Commands.RemoveWishlistItem;

internal sealed class RemoveWishlistItemCommandHandler : ICommandHandler<RemoveWishlistItemCommand>
{
    private readonly IWishlistRepository _wishlistRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public RemoveWishlistItemCommandHandler(
        IWishlistRepository wishlistRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _wishlistRepository = wishlistRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RemoveWishlistItemCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result.Failure(WishlistErrors.InvalidCustomerId);
        }

        if (request.ProductId == Guid.Empty)
        {
            return Result.Failure(WishlistErrors.InvalidProductId);
        }

        var item = await _wishlistRepository.GetByCustomerAndProductAsync(
            request.CustomerId,
            request.ProductId,
            cancellationToken);

        if (item is null)
        {
            return Result.Failure(WishlistErrors.NotFound);
        }

        _wishlistRepository.Remove(item);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
