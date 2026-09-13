using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Wishlist;
using EnterpriseCommerce.Application.Marketing.Wishlist.Commands.AddWishlistItem;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Commands;

public class AddWishlistItemCommandHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepositoryMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly AddWishlistItemCommandHandler _handler;

    public AddWishlistItemCommandHandlerTests()
    {
        _wishlistRepositoryMock = new Mock<IWishlistRepository>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();

        _timeProviderMock
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero));

        _handler = new AddWishlistItemCommandHandler(
            _wishlistRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WhenProductIsActiveAndNotWishlisted_ShouldSucceedAndSaveOnce()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = Product.Create("Test Product", "SKU-001", 100m, "TWD").Value;

        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _wishlistRepositoryMock
            .Setup(r => r.GetByCustomerAndProductAsync(customerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WishlistItem?)null);

        var command = new AddWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _wishlistRepositoryMock.Verify(r => r.Add(It.Is<WishlistItem>(item =>
            item.CustomerId == customerId &&
            item.ProductId == productId &&
            item.AddedAt == new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero)
        )), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldFailWithNotFoundAndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var command = new AddWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
        _wishlistRepositoryMock.Verify(r => r.Add(It.IsAny<WishlistItem>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductIsInactive_ShouldFailWithNotFoundAndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = Product.Create("Test Product", "SKU-001", 100m, "TWD").Value;
        product.Deactivate();

        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = new AddWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ProductErrors.NotFound.Code);
        _wishlistRepositoryMock.Verify(r => r.Add(It.IsAny<WishlistItem>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenItemAlreadyExists_ShouldFailWithAlreadyExistsAndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var product = Product.Create("Test Product", "SKU-001", 100m, "TWD").Value;
        var existingItem = WishlistItem.Create(Guid.NewGuid(), customerId, productId, DateTimeOffset.UtcNow).Value;

        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _wishlistRepositoryMock
            .Setup(r => r.GetByCustomerAndProductAsync(customerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingItem);

        var command = new AddWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.AlreadyExists.Code);
        _wishlistRepositoryMock.Verify(r => r.Add(It.IsAny<WishlistItem>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerIdIsEmpty_ShouldFailWithInvalidCustomerIdAndNotSave()
    {
        // Arrange
        var customerId = Guid.Empty;
        var productId = Guid.NewGuid();
        var command = new AddWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.InvalidCustomerId.Code);
        _productRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductIdIsEmpty_ShouldFailWithInvalidProductIdAndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.Empty;
        var command = new AddWishlistItemCommand(customerId, productId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(WishlistErrors.InvalidProductId.Code);
        _productRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
