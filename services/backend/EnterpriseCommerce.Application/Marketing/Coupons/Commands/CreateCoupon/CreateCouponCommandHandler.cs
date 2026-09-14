using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Exceptions;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Coupons.Commands.CreateCoupon;

internal sealed class CreateCouponCommandHandler : ICommandHandler<CreateCouponCommand, CouponResponse>
{
    private readonly ICouponRepository _couponRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateCouponCommandHandler(
        ICouponRepository couponRepository,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _couponRepository = couponRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<CouponResponse>> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Result.Failure<CouponResponse>(CouponErrors.InvalidCode);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();

        var exists = await _couponRepository.ExistsByNormalizedCodeAsync(normalizedCode, cancellationToken);
        if (exists)
        {
            return Result.Failure<CouponResponse>(CouponErrors.Conflict);
        }

        var now = _timeProvider.GetUtcNow();
        var couponResult = Coupon.Create(
            Guid.NewGuid(),
            normalizedCode,
            request.DiscountAmount,
            request.Currency,
            request.StartsAt,
            request.ExpiresAt,
            now);

        if (couponResult.IsFailure)
        {
            return Result.Failure<CouponResponse>(couponResult.Error);
        }

        var coupon = couponResult.Value;
        _couponRepository.Add(coupon);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (CouponCodeConflictException)
        {
            return Result.Failure<CouponResponse>(CouponErrors.Conflict);
        }


        var response = new CouponResponse(
            coupon.Id,
            coupon.Code,
            coupon.DiscountAmount,
            coupon.Currency,
            coupon.StartsAt,
            coupon.ExpiresAt,
            coupon.IsActive,
            coupon.CreatedAt);

        return Result.Success(response);
    }
}
