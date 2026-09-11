using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetCustomerOrders;

internal sealed class GetCustomerOrdersQueryHandler
    : IQueryHandler<GetCustomerOrdersQuery, CustomerOrderPageResponse>
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly IOrderRepository _orderRepository;

    public GetCustomerOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<CustomerOrderPageResponse>> Handle(
        GetCustomerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedPage = request.Page <= 0 ? DefaultPage : request.Page;
        var normalizedPageSize = request.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(request.PageSize, MaxPageSize);

        var (orders, totalCount) = await _orderRepository.GetCustomerOrderHistoryAsync(
            request.CustomerId,
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        var items = orders.Select(order => new CustomerOrderSummaryResponse(
            order.Id.Value,
            order.Status.ToString(),
            order.SubmittedAt!.Value,
            order.TotalAmount.Amount,
            order.Currency)).ToList();

        var response = new CustomerOrderPageResponse(
            items,
            normalizedPage,
            normalizedPageSize,
            totalCount);

        return Result.Success(response);
    }
}
