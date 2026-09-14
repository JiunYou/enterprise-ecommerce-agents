using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Orders.Commands.RemoveCouponFromCart;

internal sealed class RemoveCouponFromCartCommandHandler : ICommandHandler<RemoveCouponFromCartCommand, CartResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public RemoveCouponFromCartCommandHandler(
        IOrderRepository orderRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CartResponse>> Handle(RemoveCouponFromCartCommand request, CancellationToken cancellationToken)
    {
        var pendingOrder = await _orderRepository.GetPendingOrderByCustomerIdAsync(request.CustomerId, cancellationToken);
        if (pendingOrder is null || pendingOrder.CustomerId != request.CustomerId)
        {
            return Result.Failure<CartResponse>(OrderErrors.NotFound);
        }

        var removeResult = pendingOrder.RemoveCoupon();
        if (removeResult.IsFailure)
        {
            return Result.Failure<CartResponse>(removeResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
            items,
            pendingOrder.SubtotalAmount.Amount,
            pendingOrder.DiscountAmount.Amount,
            pendingOrder.AppliedCouponCode);

        return Result.Success(response);
    }
}
