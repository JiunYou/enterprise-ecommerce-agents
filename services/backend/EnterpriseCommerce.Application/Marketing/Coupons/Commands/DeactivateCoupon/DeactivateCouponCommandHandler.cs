using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Coupons.Commands.DeactivateCoupon;

internal sealed class DeactivateCouponCommandHandler : ICommandHandler<DeactivateCouponCommand>
{
    private readonly ICouponRepository _couponRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public DeactivateCouponCommandHandler(
        ICouponRepository couponRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _couponRepository = couponRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeactivateCouponCommand request, CancellationToken cancellationToken)
    {
        var coupon = await _couponRepository.GetByIdAsync(request.Id, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure(CouponErrors.NotFound);
        }

        var deactivateResult = coupon.Deactivate();
        if (deactivateResult.IsFailure)
        {
            return Result.Failure(deactivateResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
