using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Marketing.Coupons.Commands.DeactivateCoupon;
using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Commands;

public class DeactivateCouponCommandHandlerTests
{
    private readonly Mock<ICouponRepository> _couponRepositoryMock = new();
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock = new();
    private readonly Guid _couponId = Guid.NewGuid();

    [Fact]
    public async Task Handle_WhenActiveCouponExists_ShouldDeactivateAndSaveOnce()
    {
        // Arrange
        var coupon = Coupon.Create(
            _couponId,
            "WELCOME100",
            100m,
            "USD",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow).Value;

        _couponRepositoryMock.Setup(r => r.GetByIdAsync(_couponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new DeactivateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new DeactivateCouponCommand(_couponId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        coupon.IsActive.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenCouponNotFound_ShouldFailWithNotFound()
    {
        // Arrange
        _couponRepositoryMock.Setup(r => r.GetByIdAsync(_couponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Coupon?)null);

        var handler = new DeactivateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new DeactivateCouponCommand(_couponId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.NotFound);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenAlreadyDeactivated_ShouldFail()
    {
        // Arrange
        var coupon = Coupon.Create(
            _couponId,
            "WELCOME100",
            100m,
            "USD",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(7),
            DateTimeOffset.UtcNow).Value;
        coupon.Deactivate();

        _couponRepositoryMock.Setup(r => r.GetByIdAsync(_couponId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(coupon);

        var handler = new DeactivateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new DeactivateCouponCommand(_couponId), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.AlreadyDeactivated);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
