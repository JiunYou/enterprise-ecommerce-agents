using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;

internal sealed class GetAdminOrderByIdQueryHandler : IQueryHandler<GetAdminOrderByIdQuery, AdminOrderDetailResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAdminOrderCancellationStore _adminOrderCancellationStore;

    public GetAdminOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IAdminOrderCancellationStore adminOrderCancellationStore)
    {
        _orderRepository = orderRepository;
        _adminOrderCancellationStore = adminOrderCancellationStore;
    }

    public async Task<Result<AdminOrderDetailResponse>> Handle(GetAdminOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<AdminOrderDetailResponse>(OrderErrors.NotFound);
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

        var audit = await _adminOrderCancellationStore.GetByOrderIdAsync(order.Id.Value, cancellationToken);
        AdminCancellationResponse? adminCancellation = audit is not null
            ? new AdminCancellationResponse(
                audit.ActorIssuer,
                audit.ActorSubject,
                audit.CancelledAt,
                audit.Reason)
            : null;

        var response = new AdminOrderDetailResponse(
            order.Id.Value,
            order.CustomerId,
            order.Status.ToString(),
            order.Currency,
            order.TotalAmount.Amount,
            order.SubmittedAt,
            items,
            shippingAddress,
            adminCancellation);

        return Result.Success(response);
    }
}
