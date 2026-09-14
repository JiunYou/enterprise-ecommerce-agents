using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Marketing.Coupons.Commands.CreateCoupon;
using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Commands;

public class CreateCouponCommandHandlerTests
{
    private readonly Mock<ICouponRepository> _couponRepositoryMock = new();
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<TimeProvider> _timeProviderMock = new();
    private readonly DateTimeOffset _fixedNow = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    public CreateCouponCommandHandlerTests()
    {
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedNow);
    }

    [Fact]
    public async Task Handle_WhenValid_ShouldAddCouponAndSaveOnce()
    {
        // Arrange
        _couponRepositoryMock.Setup(r => r.ExistsByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        var command = new CreateCouponCommand(
            " welcome100 ",
            100m,
            "usd",
            _fixedNow,
            _fixedNow.AddDays(7));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("WELCOME100");
        result.Value.DiscountAmount.Should().Be(100m);
        result.Value.Currency.Should().Be("USD");
        result.Value.IsActive.Should().BeTrue();

        _couponRepositoryMock.Verify(r => r.Add(It.Is<Coupon>(c => c.Code == "WELCOME100")), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDuplicateNormalizedCodeExists_ShouldFailWithConflict()
    {
        // Arrange
        _couponRepositoryMock.Setup(r => r.ExistsByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        var command = new CreateCouponCommand(
            "WELCOME100",
            100m,
            "USD",
            _fixedNow,
            _fixedNow.AddDays(7));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.Conflict);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDomainValidationFails_ShouldFailWithoutSaving()
    {
        // Arrange
        _couponRepositoryMock.Setup(r => r.ExistsByNormalizedCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        var command = new CreateCouponCommand(
            "INVALID CODE WITH SPACES",
            100m,
            "USD",
            _fixedNow,
            _fixedNow.AddDays(7));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.InvalidCode);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUniqueConstraintConflictOccurs_ShouldFailWithCouponConflict()
    {
        // Arrange
        _couponRepositoryMock.Setup(r => r.ExistsByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EnterpriseCommerce.Application.Exceptions.CouponCodeConflictException());

        var handler = new CreateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        var command = new CreateCouponCommand(
            "WELCOME100",
            100m,
            "USD",
            _fixedNow,
            _fixedNow.AddDays(7));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CouponErrors.Conflict);
    }

    [Fact]
    public async Task Handle_WhenUnrelatedExceptionOccurs_ShouldPropagateException()
    {
        // Arrange
        _couponRepositoryMock.Setup(r => r.ExistsByNormalizedCodeAsync("WELCOME100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unrelated database error"));

        var handler = new CreateCouponCommandHandler(
            _couponRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);

        var command = new CreateCouponCommand(
            "WELCOME100",
            100m,
            "USD",
            _fixedNow,
            _fixedNow.AddDays(7));

        // Act
        var act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unrelated database error");
    }
}

