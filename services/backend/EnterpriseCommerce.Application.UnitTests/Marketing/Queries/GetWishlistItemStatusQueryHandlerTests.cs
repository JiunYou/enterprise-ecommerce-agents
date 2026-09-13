using EnterpriseCommerce.Application.Marketing.Wishlist;
using EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlistItemStatus;
using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Queries;

public class GetWishlistItemStatusQueryHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepositoryMock;
    private readonly GetWishlistItemStatusQueryHandler _handler;

    public GetWishlistItemStatusQueryHandlerTests()
    {
        _wishlistRepositoryMock = new Mock<IWishlistRepository>();
        _handler = new GetWishlistItemStatusQueryHandler(_wishlistRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenItemExists_ShouldReturnIsWishlistedTrue()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var query = new GetWishlistItemStatusQuery(customerId, productId);

        _wishlistRepositoryMock
            .Setup(r => r.ExistsAsync(customerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.IsWishlisted.Should().BeTrue();
        _wishlistRepositoryMock.Verify(r => r.ExistsAsync(customerId, productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenItemDoesNotExist_ShouldReturnIsWishlistedFalse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var query = new GetWishlistItemStatusQuery(customerId, productId);

        _wishlistRepositoryMock
            .Setup(r => r.ExistsAsync(customerId, productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.IsWishlisted.Should().BeFalse();
        _wishlistRepositoryMock.Verify(r => r.ExistsAsync(customerId, productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomerIsolation_ShouldQueryRepositoryWithBothCustomerIdAndProductId()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var productX = Guid.NewGuid();

        // Customer A owns row, Customer B does not
        _wishlistRepositoryMock
            .Setup(r => r.ExistsAsync(customerA, productX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _wishlistRepositoryMock
            .Setup(r => r.ExistsAsync(customerB, productX, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var queryB = new GetWishlistItemStatusQuery(customerB, productX);

        // Act
        var resultB = await _handler.Handle(queryB, CancellationToken.None);

        // Assert
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.IsWishlisted.Should().BeFalse();
        _wishlistRepositoryMock.Verify(r => r.ExistsAsync(customerB, productX, It.IsAny<CancellationToken>()), Times.Once);
        _wishlistRepositoryMock.Verify(r => r.ExistsAsync(customerA, productX, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerIdIsEmpty_ShouldReturnInvalidCustomerIdFailure()
    {
        // Arrange
        var query = new GetWishlistItemStatusQuery(Guid.Empty, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WishlistErrors.InvalidCustomerId);
        _wishlistRepositoryMock.Verify(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductIdIsEmpty_ShouldReturnInvalidProductIdFailure()
    {
        // Arrange
        var query = new GetWishlistItemStatusQuery(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WishlistErrors.InvalidProductId);
        _wishlistRepositoryMock.Verify(r => r.ExistsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
