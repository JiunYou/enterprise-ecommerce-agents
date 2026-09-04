using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;

internal sealed class SubmitOrderCommandHandler : ICommandHandler<SubmitOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public SubmitOrderCommandHandler(
        IOrderRepository orderRepository,
        IInventoryRepository inventoryRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(SubmitOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.ShippingAddress is null)
        {
            return Result.Failure(OrderErrors.ShippingAddressRequired);
        }

        var shippingAddressResult = ShippingAddress.Create(
            request.ShippingAddress.RecipientName,
            request.ShippingAddress.Phone,
            request.ShippingAddress.CountryCode,
            request.ShippingAddress.PostalCode,
            request.ShippingAddress.City,
            request.ShippingAddress.AddressLine1,
            request.ShippingAddress.AddressLine2);

        if (shippingAddressResult.IsFailure)
        {
            return Result.Failure(shippingAddressResult.Error);
        }

        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null || order.CustomerId != request.CustomerId)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        if (order.Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        if (order.Items.Count == 0)
        {
            return Result.Failure(OrderErrors.EmptyOrder);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Sort items deterministically by ProductId to prevent deadlocks
            var sortedItems = order.Items.OrderBy(i => i.ProductId.Value).ToList();

            // Try reserving stock for all items
            foreach (var item in sortedItems)
            {
                var productRef = new ProductReference(item.ProductId.Value);
                // Acquire InnoDB Row-Level Lock
                var inventoryItem = await _inventoryRepository.GetByProductIdForUpdateAsync(productRef, cancellationToken);
                
                if (inventoryItem is null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Failure(InventoryErrors.InsufficientStock);
                }

                var reserveResult = inventoryItem.ReserveStock(order.Id.Value, item.Quantity);
                if (reserveResult.IsFailure)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return reserveResult;
                }
            }

            var submitResult = order.Submit(shippingAddressResult.Value, DateTimeOffset.UtcNow);
            if (submitResult.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return submitResult;
            }

            // CommitTransactionAsync internally calls SaveChangesAsync
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
