using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Orders.Commands.ApplyCouponToCart;

internal sealed class ApplyCouponToCartCommandHandler : ICommandHandler<ApplyCouponToCartCommand, CartResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public ApplyCouponToCartCommandHandler(
        IOrderRepository orderRepository,
        ICouponRepository couponRepository,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _orderRepository = orderRepository;
        _couponRepository = couponRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<CartResponse>> Handle(ApplyCouponToCartCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result.Failure<CartResponse>(CouponErrors.InvalidOrUnavailable);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var pendingOrder = await _orderRepository.GetPendingOrderByCustomerIdAsync(request.CustomerId, cancellationToken);
        if (pendingOrder is null || pendingOrder.CustomerId != request.CustomerId)
        {
            return Result.Failure<CartResponse>(OrderErrors.NotFound);
        }

        var coupon = await _couponRepository.GetByNormalizedCodeAsync(normalizedCode, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure<CartResponse>(CouponErrors.InvalidOrUnavailable);
        }

        var now = _timeProvider.GetUtcNow();
        var eligibilityResult = coupon.CheckEligibility(now, pendingOrder.Currency);
        if (eligibilityResult.IsFailure)
        {
            // 依據第 18 節隱私要求，未啟用/未開始/過期/幣別不符等統一回傳安全的 InvalidOrUnavailable
            return Result.Failure<CartResponse>(CouponErrors.InvalidOrUnavailable);
        }

        var discountMoney = new Money(coupon.DiscountAmount, coupon.Currency);
        var applyResult = pendingOrder.ApplyCoupon(coupon.Code, discountMoney, coupon.ExpiresAt);
        if (applyResult.IsFailure)
        {
            return Result.Failure<CartResponse>(applyResult.Error);
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
