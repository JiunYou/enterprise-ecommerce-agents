using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.UpdateCartItemQuantity;

internal sealed class UpdateCartItemQuantityCommandHandler : ICommandHandler<UpdateCartItemQuantityCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateCartItemQuantityCommandHandler(
        IOrderRepository orderRepository,
        IInventoryRepository inventoryRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(UpdateCartItemQuantityCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetPendingOrderByCustomerIdAsync(request.CustomerId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(OrderErrors.ItemNotFound);
        }

        var productId = new ProductId(request.ProductId);
        var item = order.Items.FirstOrDefault(i => i.ProductId == productId);

        if (item is null)
        {
            return Result.Failure(OrderErrors.ItemNotFound);
        }

        var productRef = new ProductReference(request.ProductId);
        var inventory = await _inventoryRepository.GetByProductIdAsync(productRef, cancellationToken);

        if (inventory is null || inventory.AvailableQuantity.Value < request.Quantity)
        {
            return Result.Failure(InventoryErrors.InsufficientStock);
        }

        var updateResult = order.UpdateItemQuantity(productId, request.Quantity);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
