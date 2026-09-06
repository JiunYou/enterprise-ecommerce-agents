using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;

/// <summary>
/// 管理員取消訂單命令處理器。
/// </summary>
internal sealed class AdminCancelOrderCommandHandler : ICommandHandler<AdminCancelOrderCommand>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAdminOrderCancellationStore _adminOrderCancellationStore;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AdminCancelOrderCommandHandler(
        IOrderRepository orderRepository,
        IAdminOrderCancellationStore adminOrderCancellationStore,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _adminOrderCancellationStore = adminOrderCancellationStore;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(AdminCancelOrderCommand request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure(OrderErrors.NotFound);
        }

        // 管理員取消策略：僅允許 Pending 與 Submitted 狀態
        if (order.Status == OrderStatus.Paid)
        {
            return Result.Failure(new Error("Order.CannotCancelPaidOrder", "Paid orders cannot be cancelled by this operation."));
        }

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Submitted)
        {
            return Result.Failure(OrderErrors.InvalidStatusTransition);
        }

        var cancelResult = order.Cancel();
        if (cancelResult.IsFailure)
        {
            return cancelResult;
        }

        var cancelledAt = _timeProvider.GetUtcNow();
        var trimmedReason = request.Reason.Trim();

        var audit = new AdminOrderCancellationAudit(
            order.Id.Value,
            request.ActorIssuer,
            request.ActorSubject,
            cancelledAt,
            trimmedReason);

        _adminOrderCancellationStore.Add(audit);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.GetType().Name == "DbUpdateConcurrencyException" || ex.GetType().FullName?.Contains("DbUpdateConcurrencyException") == true)
        {
            // 樂觀並發衝突，不於中毒之 DbContext 上重試，直接回傳 Conflict 錯誤
            return Result.Failure(new Error("Order.ConcurrencyConflict", "The order was modified by another operation."));
        }

        return Result.Success();
    }
}
