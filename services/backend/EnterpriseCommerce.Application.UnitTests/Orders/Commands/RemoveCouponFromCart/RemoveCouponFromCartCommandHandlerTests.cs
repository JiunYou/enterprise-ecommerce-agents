using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.RemoveCouponFromCart;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.RemoveCouponFromCart;

public class RemoveCouponFromCartCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly ProductId _productId = new(Guid.NewGuid());

    [Fact]
    public async Task Handle_WhenOrderHasAppliedCoupon_ShouldRemoveCouponAndSaveOnce()
    {
        // Arrange
        var order = Order.Create(_customerId, "USD");
        order.AddItem(_productId, new Money(500m, "USD"), 2);
        order.ApplyCoupon("WELCOME100", new Money(100m, "USD"), DateTimeOffset.UtcNow.AddDays(7));

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new RemoveCouponFromCartCommandHandler(
            _orderRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new RemoveCouponFromCartCommand(_customerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AppliedCouponCode.Should().BeNull();
        result.Value.DiscountAmount.Should().Be(0m);
        result.Value.TotalAmount.Should().Be(1000m);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldFailWithOrderNotFound()
    {
        // Arrange
        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(_customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new RemoveCouponFromCartCommandHandler(
            _orderRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new RemoveCouponFromCartCommand(_customerId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(OrderErrors.NotFound);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
