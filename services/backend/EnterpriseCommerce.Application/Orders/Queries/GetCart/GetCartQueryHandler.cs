using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetCart;

internal sealed class GetCartQueryHandler : IQueryHandler<GetCartQuery, CartResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetCartQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Result<CartResponse>> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        var pendingOrder = await _orderRepository.GetPendingOrderByCustomerIdAsync(request.CustomerId, cancellationToken);

        if (pendingOrder is null)
        {
            return Result.Success(CartResponse.Empty());
        }

        var items = pendingOrder.Items.Select(item => new CartItemResponse(
            item.ProductId.Value,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency,
            item.Quantity,
            item.GetTotalPrice().Amount)).ToList();

        var response = new CartResponse(
            pendingOrder.Id.Value,
            pendingOrder.Currency,
            pendingOrder.TotalAmount.Amount,
            items);

        return Result.Success(response);
    }
}
