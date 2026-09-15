using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderOperationsOverview;

internal sealed class GetAdminOrderOperationsOverviewQueryHandler
    : IQueryHandler<GetAdminOrderOperationsOverviewQuery, AdminOrderOperationsOverviewResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetAdminOrderOperationsOverviewQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<AdminOrderOperationsOverviewResponse>> Handle(
        GetAdminOrderOperationsOverviewQuery request,
        CancellationToken cancellationToken)
    {
        var data = await _orderRepository.GetAdminOrderOperationsOverviewAsync(cancellationToken);

        var recentOrders = data.RecentOrders
            .Select(r => new AdminOrderOperationsOverviewRecentOrder(
                r.Id,
                r.Status,
                r.Currency,
                r.TotalAmount,
                r.SubmittedAt))
            .ToList();

        var response = new AdminOrderOperationsOverviewResponse(
            data.TotalCount,
            data.SubmittedCount,
            data.PaidCount,
            data.ShippedCount,
            data.CancelledCount,
            recentOrders);

        return Result.Success(response);
    }
}
