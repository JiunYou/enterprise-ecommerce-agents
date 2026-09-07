using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetCustomerOrders;

internal sealed class GetCustomerOrdersQueryHandler
    : IQueryHandler<GetCustomerOrdersQuery, IReadOnlyList<CustomerOrderSummaryResponse>>
{
    private readonly IOrderRepository _orderRepository;

    public GetCustomerOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<IReadOnlyList<CustomerOrderSummaryResponse>>> Handle(
        GetCustomerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetCustomerOrderHistoryAsync(
            request.CustomerId,
            cancellationToken);

        var response = orders.Select(order => new CustomerOrderSummaryResponse(
            order.Id.Value,
            order.Status.ToString(),
            order.SubmittedAt!.Value,
            order.TotalAmount.Amount,
            order.Currency)).ToList();

        return Result.Success<IReadOnlyList<CustomerOrderSummaryResponse>>(response);
    }
}
