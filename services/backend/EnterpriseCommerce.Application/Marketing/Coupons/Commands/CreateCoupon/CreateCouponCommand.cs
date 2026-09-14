using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Marketing.Coupons;
using System;

namespace EnterpriseCommerce.Application.Marketing.Coupons.Commands.CreateCoupon;

public sealed record CreateCouponCommand(
    string Code,
    decimal DiscountAmount,
    string Currency,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt) : ICommand<CouponResponse>;
