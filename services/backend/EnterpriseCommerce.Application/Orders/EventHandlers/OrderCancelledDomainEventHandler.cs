using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Application.Inventory.Commands.ReleaseInventory;
using EnterpriseCommerce.Domain.Orders.Events;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseCommerce.Application.Orders.EventHandlers;

internal sealed class OrderCancelledDomainEventHandler : IDomainEventHandler<OrderStatusChangedDomainEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISender _sender;
    private readonly ILogger<OrderCancelledDomainEventHandler> _logger;

    public OrderCancelledDomainEventHandler(
        IOrderRepository orderRepository, 
        ISender sender,
        ILogger<OrderCancelledDomainEventHandler> logger)
    {
        _orderRepository = orderRepository;
        _sender = sender;
        _logger = logger;
    }

    public async Task HandleAsync(OrderStatusChangedDomainEvent notification, CancellationToken cancellationToken = default)
    {
        if (notification.NewStatus != OrderStatus.Cancelled)
        {
            return;
        }

        var order = await _orderRepository.GetByIdAsync(notification.OrderId, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found when handling OrderStatusChangedDomainEvent", notification.OrderId.Value);
            return;
        }

        foreach (var item in order.Items)
        {
            var command = new ReleaseInventoryReservationCommand(item.ProductId.Value, order.Id.Value);
            
            var result = await _sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                // In an Outbox-driven system, we typically let it throw to retry, 
                // but since we are sending commands synchronously here within the same transaction scope, 
                // or via Outbox if MediatR is configured differently, we should throw if it fails 
                // to rollback the transaction or retry.
                // Assuming ReleaseInventoryReservationCommandHandler returns failure for Domain errors,
                // we log and throw to ensure the transaction doesn't commit if inventory release fails.
                _logger.LogError("Failed to release inventory reservation for Order {OrderId}, Product {ProductId}: {Error}", order.Id.Value, item.ProductId.Value, result.Error);
                throw new InvalidOperationException($"Failed to release inventory: {result.Error.Message}");
            }
        }
    }
}
