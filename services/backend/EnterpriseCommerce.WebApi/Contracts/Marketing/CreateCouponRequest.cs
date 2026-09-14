using System;

namespace EnterpriseCommerce.WebApi.Contracts.Marketing;

public sealed record CreateCouponRequest(
    string Code,
    decimal DiscountAmount,
    string Currency,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt);
