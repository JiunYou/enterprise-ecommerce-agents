using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Linq;

namespace EnterpriseCommerce.Domain.Marketing;

/// <summary>
/// 優惠券領域實體 (Marketing Context)
/// </summary>
public sealed class Coupon : Entity<Guid>
{
    private Coupon(
        Guid id,
        string code,
        decimal discountAmount,
        string currency,
        DateTimeOffset startsAt,
        DateTimeOffset expiresAt,
        bool isActive,
        DateTimeOffset createdAt)
        : base(id)
    {
        Code = code;
        DiscountAmount = discountAmount;
        Currency = currency;
        StartsAt = startsAt;
        ExpiresAt = expiresAt;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    private Coupon()
    {
    }

    public string Code { get; private set; } = string.Empty;
    public decimal DiscountAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// 建立優惠券領域工廠方法 (自動生成 Id)
    /// </summary>
    public static Result<Coupon> Create(
        string code,
        decimal discountAmount,
        string currency,
        DateTimeOffset startsAt,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt) =>
        Create(Guid.NewGuid(), code, discountAmount, currency, startsAt, expiresAt, createdAt);

    /// <summary>
    /// 建立優惠券領域工廠方法
    /// </summary>
    public static Result<Coupon> Create(
        Guid id,
        string code,
        decimal discountAmount,
        string currency,
        DateTimeOffset startsAt,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidId);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidCode);
        }

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length < 3 || normalizedCode.Length > 32)
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidCode);
        }

        // 僅允許 ASCII A-Z, 0-9, "-"
        if (!normalizedCode.All(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-'))
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidCode);
        }

        if (discountAmount <= 0)
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidDiscountAmount);
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidCurrency);
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(c => c >= 'A' && c <= 'Z'))
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidCurrency);
        }

        if (expiresAt <= startsAt)
        {
            return Result.Failure<Coupon>(CouponErrors.InvalidTimeWindow);
        }

        var coupon = new Coupon(
            id,
            normalizedCode,
            discountAmount,
            normalizedCurrency,
            startsAt,
            expiresAt,
            true,
            createdAt);

        return Result.Success(coupon);
    }

    /// <summary>
    /// 停用優惠券
    /// </summary>
    public Result Deactivate()
    {
        if (!IsActive)
        {
            return Result.Failure(CouponErrors.AlreadyDeactivated);
        }

        IsActive = false;
        return Result.Success();
    }

    /// <summary>
    /// 檢查優惠券使用資格
    /// </summary>
    public Result CheckEligibility(DateTimeOffset now, string expectedCurrency)
    {
        if (!IsActive)
        {
            return Result.Failure(CouponErrors.Inactive);
        }

        if (now < StartsAt)
        {
            return Result.Failure(CouponErrors.NotStarted);
        }

        if (now >= ExpiresAt)
        {
            return Result.Failure(CouponErrors.Expired);
        }

        if (!string.Equals(Currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(CouponErrors.CurrencyMismatch);
        }

        return Result.Success();
    }
}
