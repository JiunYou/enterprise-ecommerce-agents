using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Marketing.Coupons.Queries.GetCoupons;
using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Queries;

public class GetCouponsQueryHandlerTests
{
    private readonly Mock<ICouponRepository> _couponRepositoryMock = new();

    [Fact]
    public async Task Handle_ShouldReturnCouponsList()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var coupon1 = Coupon.Create(Guid.NewGuid(), "COUPON1", 100m, "USD", now, now.AddDays(7), now).Value;
        var coupon2 = Coupon.Create(Guid.NewGuid(), "COUPON2", 50m, "USD", now, now.AddDays(7), now).Value;

        _couponRepositoryMock.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Coupon> { coupon1, coupon2 });

        var handler = new GetCouponsQueryHandler(_couponRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetCouponsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Code.Should().Be("COUPON1");
        result.Value[1].Code.Should().Be("COUPON2");
    }
}
