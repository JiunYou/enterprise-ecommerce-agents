using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using System;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Marketing;

public class CouponTests
{
    private readonly Guid _validId = Guid.NewGuid();
    private const string _validCode = "WELCOME100";
    private const decimal _validDiscount = 100m;
    private const string _validCurrency = "USD";
    private readonly DateTimeOffset _now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _startsAt = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DateTimeOffset _expiresAt = new(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);
    private readonly DateTimeOffset _createdAt = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_ShouldSucceed_AndSetIsActiveTrue()
    {
        // Act
        var result = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Id.Should().Be(_validId);
        result.Value.Code.Should().Be("WELCOME100");
        result.Value.DiscountAmount.Should().Be(100m);
        result.Value.Currency.Should().Be("USD");
        result.Value.StartsAt.Should().Be(_startsAt);
        result.Value.ExpiresAt.Should().Be(_expiresAt);
        result.Value.IsActive.Should().BeTrue();
        result.Value.CreatedAt.Should().Be(_createdAt);
    }

    [Theory]
    [InlineData(" welcome100 ", "WELCOME100")]
    [InlineData("sep-2026", "SEP-2026")]
    [InlineData("  DISCOUNT-50  ", "DISCOUNT-50")]
    public void Create_ShouldTrimAndUppercaseCode(string inputCode, string expectedCode)
    {
        // Act
        var result = Coupon.Create(
            _validId,
            inputCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("AB")] // 長度小於 3
    [InlineData("A123456789012345678901234567890123")] // 長度大於 32
    public void Create_WithInvalidLengthCode_ShouldFail(string invalidCode)
    {
        // Act
        var result = Coupon.Create(
            _validId,
            invalidCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidCode);
    }

    [Theory]
    [InlineData("WELCOME 100")] // 內部空白
    [InlineData("DISCOUNT_100")] // 底線不支援
    [InlineData("SAVE@100")] // 特殊符號
    [InlineData("優惠券100")] // 非 ASCII
    public void Create_WithInvalidCharactersInCode_ShouldFail(string invalidCode)
    {
        // Act
        var result = Coupon.Create(
            _validId,
            invalidCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNonPositiveDiscountAmount_ShouldFail(decimal invalidDiscount)
    {
        // Act
        var result = Coupon.Create(
            _validId,
            _validCode,
            invalidDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidDiscountAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("US")] // 長度非 3
    [InlineData("USDT")] // 長度非 3
    [InlineData("U12")] // 包含數字
    [InlineData("NT$")] // 包含符號
    public void Create_WithInvalidCurrency_ShouldFail(string? invalidCurrency)
    {
        // Act
        var result = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            invalidCurrency!,
            _startsAt,
            _expiresAt,
            _createdAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidCurrency);
    }

    [Fact]
    public void Create_WithExpiresAtNotGreaterThanStartsAt_ShouldFail()
    {
        // 結束時間等於或小於開始時間
        var result1 = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _startsAt, // 相等
            _createdAt);

        var result2 = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _startsAt.AddDays(-1), // 小於
            _createdAt);

        // Assert
        result1.IsFailure.Should().BeTrue();
        result1.Error.Should().Be(CouponErrors.InvalidTimeWindow);

        result2.IsFailure.Should().BeTrue();
        result2.Error.Should().Be(CouponErrors.InvalidTimeWindow);
    }

    [Fact]
    public void Deactivate_WhenActive_ShouldSucceed_AndSetIsActiveFalse()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);
        createResult.IsSuccess.Should().BeTrue();
        var coupon = createResult.Value;

        // Act
        var deactivateResult = coupon.Deactivate();

        // Assert
        deactivateResult.IsSuccess.Should().BeTrue();
        coupon.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Deactivate_WhenAlreadyDeactivated_ShouldFailPredictably()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);
        var coupon = createResult.Value;
        coupon.Deactivate();

        // Act
        var secondDeactivate = coupon.Deactivate();

        // Assert
        secondDeactivate.IsFailure.Should().BeTrue();
        secondDeactivate.Error.Should().Be(CouponErrors.AlreadyDeactivated);
    }

    [Fact]
    public void CheckEligibility_WhenActiveAndWithinWindowAndMatchingCurrency_ShouldSucceed()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);
        var coupon = createResult.Value;

        // Act
        var result = coupon.CheckEligibility(_now, "USD");

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CheckEligibility_WhenInactive_ShouldFail()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _startsAt,
            _expiresAt,
            _createdAt);
        var coupon = createResult.Value;
        coupon.Deactivate();

        // Act
        var result = coupon.CheckEligibility(_now, "USD");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.Inactive);
    }

    [Fact]
    public void CheckEligibility_WhenNotStarted_ShouldFail()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _now.AddHours(1),
            _now.AddDays(5),
            _createdAt);
        var coupon = createResult.Value;

        // Act
        var result = coupon.CheckEligibility(_now, "USD");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.NotStarted);
    }

    [Fact]
    public void CheckEligibility_WhenExpired_ShouldFail()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            _validCurrency,
            _now.AddDays(-10),
            _now, // now == expiresAt (邊界，StartsAt <= now < ExpiresAt)
            _createdAt);
        var coupon = createResult.Value;

        // Act
        var result = coupon.CheckEligibility(_now, "USD");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.Expired);
    }

    [Fact]
    public void CheckEligibility_WhenCurrencyMismatch_ShouldFail()
    {
        // Arrange
        var createResult = Coupon.Create(
            _validId,
            _validCode,
            _validDiscount,
            "USD",
            _startsAt,
            _expiresAt,
            _createdAt);
        var coupon = createResult.Value;

        // Act
        var result = coupon.CheckEligibility(_now, "TWD");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.CurrencyMismatch);
    }
}
