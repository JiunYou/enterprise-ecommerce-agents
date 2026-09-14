using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.ApplyCouponToCart;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.ApplyCouponToCart;

public class ApplyCouponToCartCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<ICouponRepository> _couponRepositoryMock = new();
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<TimeProvider> _timeProviderMock = new();
    private readonly DateTimeOffset _fixedNow = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly ProductId _productId = new(Guid.NewGuid());

    public ApplyCouponToCartCommandHandlerTests()
    {
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedNow);
    }

    private Order CreateCartWithItem(decimal price = 500m, int qty = 2, string currency = "USD")
    {
        var order = Order.Create(_customerId, currency);
        order.AddItem(_productId, new Money(price, currency), qty);
        return order;
    }

    private Coupon CreateValidCoupon(string code = "WELCOME100", decimal discount = 100m, string currency = "USD")
    {
        return Coupon.Create(
            Guid.NewGuid(),
            code,
            discount,
            currency,
            _fixedNow.AddDays(-1),
            _fixedNow.AddDays(7),
            _fixedNow.AddDays(-2)).Value;
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldApplyCouponAndCallSaveChangesOnce()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2); // 1000 USD
        var coupon = CreateValidCoupon("WELCOME100", 100m);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, " welcome100 "), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AppliedCouponCode.Should().Be("WELCOME100");
        result.Value.DiscountAmount.Should().Be(100m);
        result.Value.SubtotalAmount.Should().Be(1000m);
        result.Value.TotalAmount.Should().Be(900m);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCodeHasWhitespaceOrLowercase_ShouldLookupByNormalizedCode()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2);
        var coupon = CreateValidCoupon("WELCOME100", 100m);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "  welcome100  "), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _couponRepositoryMock.Verify(r => r.GetByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCouponNotFound_ShouldFailWithInvalidOrUnavailable_AndZeroSaveChanges()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Coupon?)null);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "NONEXISTENT"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidOrUnavailable);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCouponInactive_ShouldFailWithInvalidOrUnavailable()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2);
        var coupon = CreateValidCoupon("WELCOME100", 100m);
        coupon.Deactivate();

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "WELCOME100"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidOrUnavailable);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCouponNotStarted_ShouldFailWithInvalidOrUnavailable()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2);
        var coupon = Coupon.Create(
            Guid.NewGuid(),
            "FUTURE100",
            100m,
            "USD",
            _fixedNow.AddDays(1),
            _fixedNow.AddDays(10),
            _fixedNow).Value;

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("FUTURE100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "FUTURE100"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidOrUnavailable);
    }

    [Fact]
    public async Task Handle_WhenCouponExpired_ShouldFailWithInvalidOrUnavailable()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2);
        var coupon = Coupon.Create(
            Guid.NewGuid(),
            "EXPIRED100",
            100m,
            "USD",
            _fixedNow.AddDays(-10),
            _fixedNow.AddDays(-1),
            _fixedNow.AddDays(-11)).Value;

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("EXPIRED100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "EXPIRED100"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidOrUnavailable);
    }

    [Fact]
    public async Task Handle_WhenCurrencyMismatch_ShouldFailWithInvalidOrUnavailable()
    {
        // Arrange
        var order = CreateCartWithItem(500m, 2, "USD");
        var coupon = CreateValidCoupon("WELCOME100", 100m, "TWD");

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "WELCOME100"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidOrUnavailable);
    }

    [Fact]
    public async Task Handle_WhenDiscountEqualOrGreaterThanSubtotal_ShouldFail()
    {
        // Arrange
        var order = CreateCartWithItem(50m, 1); // subtotal = 50 USD
        var coupon = CreateValidCoupon("BIG100", 100m); // discount = 100 USD

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _couponRepositoryMock.Setup(r => r.GetByNormalizedCodeAsync("BIG100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "BIG100"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.CouponDiscountExceedsSubtotal);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCartNotFound_ShouldFailWithOrderNotFound()
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new ApplyCouponToCartCommandHandler(
            _orderRepositoryMock.Object,
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        // Act
        var result = await handler.Handle(new ApplyCouponToCartCommand(_customerId, "WELCOME100"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.NotFound);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
