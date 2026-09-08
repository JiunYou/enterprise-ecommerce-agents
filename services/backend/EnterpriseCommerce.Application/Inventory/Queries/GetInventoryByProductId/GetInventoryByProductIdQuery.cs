using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Inventory.Queries.GetInventoryByProductId;

public sealed record InventoryResponse(Guid ProductId, int AvailableQuantity, int ReservedQuantity);

public sealed record GetInventoryByProductIdQuery(Guid ProductId) : IQuery<InventoryResponse>;

internal sealed class GetInventoryByProductIdQueryHandler : IQueryHandler<GetInventoryByProductIdQuery, InventoryResponse>
{
    private readonly IInventoryRepository _inventoryRepository;

    public GetInventoryByProductIdQueryHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<Result<InventoryResponse>> Handle(GetInventoryByProductIdQuery request, CancellationToken cancellationToken)
    {
        var productReference = new ProductReference(request.ProductId);
        var inventoryItem = await _inventoryRepository.GetByProductIdAsync(productReference, cancellationToken);

        if (inventoryItem is null)
        {
            return Result.Failure<InventoryResponse>(InventoryErrors.NotFound);
        }

        return Result.Success(new InventoryResponse(
            request.ProductId,
            inventoryItem.AvailableQuantity.Value,
            inventoryItem.ReservedQuantity.Value));
    }
}
