using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

internal sealed class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<OrderResponse>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null || order.CustomerId != request.CustomerId)
        {
            return Result.Failure<OrderResponse>(OrderErrors.NotFound);
        }

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

        var response = new OrderResponse(
            order.Id.Value,
            order.CustomerId,
            order.Status.ToString(),
            order.Currency,
            order.TotalAmount.Amount,
            items,
            shippingAddress);

        return Result.Success(response);
    }
}
