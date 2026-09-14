using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Domain.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Coupons.Queries.GetCoupons;

internal sealed class GetCouponsQueryHandler : IQueryHandler<GetCouponsQuery, IReadOnlyList<CouponResponse>>
{
    private readonly ICouponRepository _couponRepository;

    public GetCouponsQueryHandler(ICouponRepository couponRepository)
    {
        _couponRepository = couponRepository;
    }

    public async Task<Result<IReadOnlyList<CouponResponse>>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
    {
        var coupons = await _couponRepository.GetAllAsync(cancellationToken);

        var response = coupons.Select(c => new CouponResponse(
            c.Id,
            c.Code,
            c.DiscountAmount,
            c.Currency,
            c.StartsAt,
            c.ExpiresAt,
            c.IsActive,
            c.CreatedAt)).ToList();

        return Result.Success<IReadOnlyList<CouponResponse>>(response);
    }
}
