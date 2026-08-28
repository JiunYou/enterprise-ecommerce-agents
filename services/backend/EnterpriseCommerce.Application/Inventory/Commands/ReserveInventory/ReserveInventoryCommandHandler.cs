using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Inventory.Commands.ReserveInventory;

internal sealed class ReserveInventoryCommandHandler : ICommandHandler<ReserveInventoryCommand>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public ReserveInventoryCommandHandler(IInventoryRepository inventoryRepository, IApplicationUnitOfWork unitOfWork)
    {
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(ReserveInventoryCommand request, CancellationToken cancellationToken)
    {
        var productReference = new ProductReference(request.ProductId);
        
        var inventoryItem = await _inventoryRepository.GetByProductIdAsync(productReference, cancellationToken);
        
        if (inventoryItem is null)
        {
            return Result.Failure(new Error("Inventory.NotFound", "Inventory item not found for the specified product."));
        }

        var stockQuantity = new StockQuantity(request.Quantity);
        var orderReference = new OrderReference(request.OrderId);
        var reserveResult = inventoryItem.ReserveStock(orderReference, stockQuantity);
        if (reserveResult.IsFailure)
        {
            return reserveResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
