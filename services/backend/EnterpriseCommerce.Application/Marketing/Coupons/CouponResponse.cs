using System;

namespace EnterpriseCommerce.Application.Marketing.Coupons;

public sealed record CouponResponse(
    Guid Id,
    string Code,
    decimal DiscountAmount,
    string Currency,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    bool IsActive,
    DateTimeOffset CreatedAt);
