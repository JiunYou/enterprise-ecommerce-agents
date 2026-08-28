using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Inventory.Commands.ReleaseInventory;

internal sealed class ReleaseInventoryReservationCommandHandler : ICommandHandler<ReleaseInventoryReservationCommand>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public ReleaseInventoryReservationCommandHandler(IInventoryRepository inventoryRepository, IApplicationUnitOfWork unitOfWork)
    {
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReleaseInventoryReservationCommand request, CancellationToken cancellationToken)
    {
        var productReference = new ProductReference(request.ProductId);
        var inventoryItem = await _inventoryRepository.GetByProductIdAsync(productReference, cancellationToken);

        if (inventoryItem is null)
        {
            // If inventory item doesn't exist, it means we don't hold any reservation.
            // In an eventual consistency scenario for release, this is an idempotent success.
            return Result.Success();
        }

        var orderReference = new OrderReference(request.OrderId);
        var releaseResult = inventoryItem.ReleaseReservation(orderReference);

        if (releaseResult.IsFailure)
        {
            return releaseResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
