using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetFulfillmentOrders;

internal sealed class GetFulfillmentOrdersQueryHandler : IQueryHandler<GetFulfillmentOrdersQuery, IReadOnlyList<OrderResponse>>
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    private readonly IOrderRepository _orderRepository;

    public GetFulfillmentOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<IReadOnlyList<OrderResponse>>> Handle(GetFulfillmentOrdersQuery request, CancellationToken cancellationToken)
    {
        var boundedLimit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var orders = await _orderRepository.GetFulfillmentQueueAsync(boundedLimit, cancellationToken);

        var responseList = orders.Select(order =>
        {
            var items = order.Items.Select(item => new OrderItemResponse(
                item.ProductId.Value,
                item.UnitPrice.Amount,
                item.UnitPrice.Currency,
                item.Quantity,
                item.GetTotalPrice().Amount)).ToList();

            ShippingAddressResponse? shippingAddress = order.ShippingAddress is not null
                ? new ShippingAddressResponse(
                    order.ShippingAddress.RecipientName,
                    order.ShippingAddress.Phone,
                    order.ShippingAddress.CountryCode,
                    order.ShippingAddress.PostalCode,
                    order.ShippingAddress.City,
                    order.ShippingAddress.AddressLine1,
                    order.ShippingAddress.AddressLine2)
                : null;

            return new OrderResponse(
                order.Id.Value,
                order.CustomerId,
                order.Status.ToString(),
                order.Currency,
                order.TotalAmount.Amount,
                items,
                shippingAddress);
        }).ToList();

        return Result.Success<IReadOnlyList<OrderResponse>>(responseList);
    }
}
