using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Wishlist;
using EnterpriseCommerce.Application.Marketing.Wishlist.Commands.RemoveWishlistItem;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Commands;

public class RemoveWishlistItemCommandHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly RemoveWishlistItemCommandHandler _handler;

    public RemoveWishlistItemCommandHandlerTests()
    {
        _wishlistRepositoryMock = new Mock<IWishlistRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new RemoveWishlistItemCommandHandler(
            _wishlistRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenItemExistsForCustomer_ShouldRemoveAndSaveOnce()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var item = WishlistItem.Create(Guid.NewGuid(), customerId, productId, DateTimeOffset.UtcNow).Value;

        _wishlistRepositoryMock
            .Setup(r => r.GetByCustomerAndProductAsync(customerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var command = new RemoveWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _wishlistRepositoryMock.Verify(r => r.Remove(item), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenItemDoesNotExistForCustomer_ShouldFailWithNotFoundAndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _wishlistRepositoryMock
            .Setup(r => r.GetByCustomerAndProductAsync(customerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WishlistItem?)null);

        var command = new RemoveWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.NotFound.Code);
        _wishlistRepositoryMock.Verify(r => r.Remove(It.IsAny<WishlistItem>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerAttemptsToRemoveAnotherCustomersItem_ShouldOnlyQueryByExecutingCustomerId()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Customer A 擁有該商品，但 Customer B 發出請求
        _wishlistRepositoryMock
            .Setup(r => r.GetByCustomerAndProductAsync(customerB, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WishlistItem?)null);

        var command = new RemoveWishlistItemCommand(customerB, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.NotFound.Code);
        _wishlistRepositoryMock.Verify(r => r.GetByCustomerAndProductAsync(customerB, productId, It.IsAny<CancellationToken>()), Times.Once);
        _wishlistRepositoryMock.Verify(r => r.GetByCustomerAndProductAsync(customerA, productId, It.IsAny<CancellationToken>()), Times.Never);
        _wishlistRepositoryMock.Verify(r => r.Remove(It.IsAny<WishlistItem>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
