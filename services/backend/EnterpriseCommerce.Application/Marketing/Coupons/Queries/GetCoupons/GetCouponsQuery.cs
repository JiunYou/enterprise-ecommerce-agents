using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Marketing.Coupons;
using System.Collections.Generic;

namespace EnterpriseCommerce.Application.Marketing.Coupons.Queries.GetCoupons;

public sealed record GetCouponsQuery : IQuery<IReadOnlyList<CouponResponse>>;
