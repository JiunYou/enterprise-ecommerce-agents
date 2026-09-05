using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;

internal sealed class GetAdminOrdersQueryHandler : IQueryHandler<GetAdminOrdersQuery, AdminOrderPageResponse>
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly IOrderRepository _orderRepository;

    public GetAdminOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<AdminOrderPageResponse>> Handle(GetAdminOrdersQuery request, CancellationToken cancellationToken)
    {
        var normalizedPage = request.Page <= 0 ? DefaultPage : request.Page;
        var normalizedPageSize = request.PageSize <= 0 
            ? DefaultPageSize 
            : Math.Min(request.PageSize, MaxPageSize);

        OrderStatus? filterStatus = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<OrderStatus>(request.Status.Trim(), ignoreCase: true, out var parsedStatus) ||
                !Enum.IsDefined(typeof(OrderStatus), parsedStatus))
            {
                return Result.Failure<AdminOrderPageResponse>(
                    new Error("AdminOrders.InvalidStatus", $"Invalid order status '{request.Status}'."));
            }

            filterStatus = parsedStatus;
        }

        OrderId? filterOrderId = request.OrderId.HasValue
            ? new OrderId(request.OrderId.Value)
            : null;

        var (orders, totalCount) = await _orderRepository.GetAdminOrdersAsync(
            filterStatus,
            filterOrderId,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        var summaryItems = orders.Select(order => new AdminOrderSummaryResponse(
            order.Id.Value,
            order.CustomerId,
            order.Status.ToString(),
            order.Currency,
            order.TotalAmount.Amount,
            order.SubmittedAt)).ToList();

        var response = new AdminOrderPageResponse(
            summaryItems,
            normalizedPage,
            normalizedPageSize,
            totalCount);

        return Result.Success(response);
    }
}
