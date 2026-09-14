using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using System;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Orders;

public class OrderCouponTests
{
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly ProductId _productId1 = new(Guid.NewGuid());
    private readonly ProductId _productId2 = new(Guid.NewGuid());
    private readonly ShippingAddress _shippingAddress = ShippingAddress.Create(
        "王小明",
        "0912345678",
        "TW",
        "100",
        "台北市",
        "中正區忠孝西路一段 1 號",
        null).Value;

    private Order CreatePendingOrderWithItems(decimal itemPrice = 500m, int quantity = 2)
    {
        var order = Order.Create(_customerId, "USD");
        order.AddItem(_productId1, new Money(itemPrice, "USD"), quantity);
        return order;
    }

    [Fact]
    public void OrderWithoutCoupon_ShouldHaveExpectedSubtotalDiscountAndTotal()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2); // 1000 USD

        // Assert
        order.SubtotalAmount.Amount.Should().Be(1000m);
        order.DiscountAmount.Amount.Should().Be(0m);
        order.TotalAmount.Amount.Should().Be(1000m);
        order.AppliedCouponCode.Should().BeNull();
        order.AppliedCouponDiscountAmount.Should().BeNull();
        order.AppliedCouponExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ApplyCoupon_WithValidFixedDiscount_ShouldUpdateDiscountAndTotal()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2); // subtotal = 1000 USD
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var result = order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.AppliedCouponCode.Should().Be("WELCOME100");
        order.AppliedCouponDiscountAmount.Should().Be(100m);
        order.AppliedCouponExpiresAt.Should().Be(expiresAt);
        order.SubtotalAmount.Amount.Should().Be(1000m);
        order.DiscountAmount.Amount.Should().Be(100m);
        order.TotalAmount.Amount.Should().Be(900m);
    }

    [Fact]
    public void ApplyCoupon_WithCurrencyMismatch_ShouldFail()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2); // USD
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var result = order.ApplyCoupon("WELCOME100", new Money(100m, "TWD"), expiresAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.CurrencyMismatch);
        order.AppliedCouponCode.Should().BeNull();
        order.DiscountAmount.Amount.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ApplyCoupon_WithZeroOrNegativeDiscount_ShouldFail(decimal invalidDiscount)
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act & Assert
        // Money 建構子若小於 0 會丟出 DomainException，若是 0 則由 ApplyCoupon 驗證
        if (invalidDiscount <= 0)
        {
            if (invalidDiscount < 0)
            {
                var act = () => new Money(invalidDiscount, "USD");
                act.Should().Throw<DomainException>();
            }
            else
            {
                var result = order.ApplyCoupon("WELCOME100", new Money(invalidDiscount, "USD"), expiresAt);
                result.IsFailure.Should().BeTrue();
                result.Error.Should().Be(OrderErrors.InvalidCouponDiscount);
            }
        }
    }

    [Theory]
    [InlineData(1000)] // 等於 subtotal
    [InlineData(1500)] // 大於 subtotal
    public void ApplyCoupon_WithDiscountEqualOrGreaterThanSubtotal_ShouldFail(decimal excessiveDiscount)
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2); // subtotal = 1000 USD
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // Act
        var result = order.ApplyCoupon("BIGDISCOUNT", new Money(excessiveDiscount, "USD"), expiresAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.CouponDiscountExceedsSubtotal);
        order.AppliedCouponCode.Should().BeNull();
    }

    [Fact]
    public void ApplyCoupon_WhenCouponAlreadyApplied_ShouldFail()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var firstResult = order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);
        firstResult.IsSuccess.Should().BeTrue();

        // Act - 嘗試套用第二張
        var secondResult = order.ApplyCoupon("SUMMER50", new Money(50m, "USD"), expiresAt);

        // Assert
        secondResult.IsFailure.Should().BeTrue();
        secondResult.Error.Should().Be(OrderErrors.CouponAlreadyApplied);
        // 第一張仍保留，不被靜默替換，也不疊加
        order.AppliedCouponCode.Should().Be("WELCOME100");
        order.DiscountAmount.Amount.Should().Be(100m);
        order.TotalAmount.Amount.Should().Be(900m);
    }

    [Fact]
    public void RemoveCoupon_WhenApplied_ShouldClearSnapshotAndResetAmounts()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);

        // Act
        var removeResult = order.RemoveCoupon();

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        order.AppliedCouponCode.Should().BeNull();
        order.AppliedCouponDiscountAmount.Should().BeNull();
        order.AppliedCouponExpiresAt.Should().BeNull();
        order.SubtotalAmount.Amount.Should().Be(1000m);
        order.DiscountAmount.Amount.Should().Be(0m);
        order.TotalAmount.Amount.Should().Be(1000m);
    }

    [Fact]
    public void CartMutation_AddItem_ShouldClearAppliedCoupon()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);

        // Act - 新增商品
        var addResult = order.AddItem(_productId2, new Money(300m, "USD"), 1);

        // Assert
        addResult.IsSuccess.Should().BeTrue();
        order.AppliedCouponCode.Should().BeNull();
        order.AppliedCouponDiscountAmount.Should().BeNull();
        order.AppliedCouponExpiresAt.Should().BeNull();
        order.DiscountAmount.Amount.Should().Be(0m);
        order.SubtotalAmount.Amount.Should().Be(1300m);
        order.TotalAmount.Amount.Should().Be(1300m);
    }

    [Fact]
    public void CartMutation_UpdateItemQuantity_ShouldClearAppliedCoupon()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);

        // Act - 變更品項數量
        var updateResult = order.UpdateItemQuantity(_productId1, 3);

        // Assert
        updateResult.IsSuccess.Should().BeTrue();
        order.AppliedCouponCode.Should().BeNull();
        order.AppliedCouponDiscountAmount.Should().BeNull();
        order.AppliedCouponExpiresAt.Should().BeNull();
        order.DiscountAmount.Amount.Should().Be(0m);
        order.SubtotalAmount.Amount.Should().Be(1500m);
    }

    [Fact]
    public void CartMutation_RemoveItem_ShouldClearAppliedCoupon()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        order.AddItem(_productId2, new Money(200m, "USD"), 1);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);

        // Act - 移除品項
        var removeResult = order.RemoveItem(_productId2);

        // Assert
        removeResult.IsSuccess.Should().BeTrue();
        order.AppliedCouponCode.Should().BeNull();
        order.AppliedCouponDiscountAmount.Should().BeNull();
        order.AppliedCouponExpiresAt.Should().BeNull();
        order.DiscountAmount.Amount.Should().Be(0m);
        order.SubtotalAmount.Amount.Should().Be(1000m);
    }

    [Fact]
    public void FailedCartMutation_ShouldNotClearAppliedCoupon()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), expiresAt);

        // Act - 失敗的數量更新 (quantity <= 0)
        var failedUpdate = order.UpdateItemQuantity(_productId1, 0);

        // Assert
        failedUpdate.IsFailure.Should().BeTrue();
        order.AppliedCouponCode.Should().Be("WELCOME100");
        order.AppliedCouponDiscountAmount.Should().Be(100m);
        order.DiscountAmount.Amount.Should().Be(100m);
    }

    [Fact]
    public void Submit_WhenAppliedCouponExpired_ShouldFailAndNotChangeStatusOrClearCoupon()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var snapshotExpiresAt = new DateTimeOffset(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), snapshotExpiresAt);

        // 伺服器時間在過期時間之後
        var serverNowAfterExpiry = new DateTimeOffset(2026, 9, 14, 10, 0, 1, TimeSpan.Zero);

        // Act
        var submitResult = order.Submit(_shippingAddress, serverNowAfterExpiry);

        // Assert
        submitResult.IsFailure.Should().BeTrue();
        submitResult.Error.Should().Be(OrderErrors.AppliedCouponExpired);
        order.Status.Should().Be(OrderStatus.Pending);
        // 不可靜默移除優惠券、不可改以原價提交
        order.AppliedCouponCode.Should().Be("WELCOME100");
        order.DiscountAmount.Amount.Should().Be(100m);
    }

    [Fact]
    public void Submit_WhenAppliedCouponValid_ShouldSucceedWithDiscountedTotal()
    {
        // Arrange
        var order = CreatePendingOrderWithItems(500m, 2);
        var snapshotExpiresAt = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), snapshotExpiresAt);

        var serverNowBeforeExpiry = new DateTimeOffset(2026, 9, 14, 11, 0, 0, TimeSpan.Zero);

        // Act
        var submitResult = order.Submit(_shippingAddress, serverNowBeforeExpiry);

        // Assert
        submitResult.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Submitted);
        order.TotalAmount.Amount.Should().Be(900m);
        order.DiscountAmount.Amount.Should().Be(100m);
    }
}
