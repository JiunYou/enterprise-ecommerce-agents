using EnterpriseCommerce.Application.Marketing.Wishlist;
using EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlist;
using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Queries;

public class GetWishlistQueryHandlerTests
{
    private readonly Mock<IWishlistRepository> _wishlistRepositoryMock;
    private readonly GetWishlistQueryHandler _handler;

    public GetWishlistQueryHandlerTests()
    {
        _wishlistRepositoryMock = new Mock<IWishlistRepository>();
        _handler = new GetWishlistQueryHandler(_wishlistRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithDefaultPagination_ShouldPassNormalizedPageAndPageSize()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var query = new GetWishlistQuery(customerId);

        _wishlistRepositoryMock
            .Setup(r => r.GetPagedByCustomerAsync(customerId, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WishlistItem>(), 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(25);
        result.Value.TotalCount.Should().Be(0);
        result.Value.Items.Should().BeEmpty();
        _wishlistRepositoryMock.Verify(r => r.GetPagedByCustomerAsync(customerId, 1, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPageSizeExceedsMaximum_ShouldClampToMaxPageSize()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var query = new GetWishlistQuery(customerId, Page: 2, PageSize: 500);

        _wishlistRepositoryMock
            .Setup(r => r.GetPagedByCustomerAsync(customerId, 2, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WishlistItem>(), 10));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(100);
        result.Value.TotalCount.Should().Be(10);
        _wishlistRepositoryMock.Verify(r => r.GetPagedByCustomerAsync(customerId, 2, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPageAndPageSizeAreZeroOrNegative_ShouldNormalizeToDefaults()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var query = new GetWishlistQuery(customerId, Page: 0, PageSize: -5);

        _wishlistRepositoryMock
            .Setup(r => r.GetPagedByCustomerAsync(customerId, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WishlistItem>(), 0));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(25);
        _wishlistRepositoryMock.Verify(r => r.GetPagedByCustomerAsync(customerId, 1, 25, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithItems_ShouldMapToProductIdsAndAddedAtExclusively()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product1 = Guid.NewGuid();
        var product2 = Guid.NewGuid();
        var time1 = new DateTimeOffset(2026, 9, 13, 14, 0, 0, TimeSpan.Zero);
        var time2 = new DateTimeOffset(2026, 9, 13, 13, 0, 0, TimeSpan.Zero);

        var item1 = WishlistItem.Create(Guid.NewGuid(), customerId, product1, time1).Value;
        var item2 = WishlistItem.Create(Guid.NewGuid(), customerId, product2, time2).Value;

        _wishlistRepositoryMock
            .Setup(r => r.GetPagedByCustomerAsync(customerId, 1, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<WishlistItem> { item1, item2 }, 2));

        var query = new GetWishlistQuery(customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Should().HaveCount(2);

        result.Value.Items[0].ProductId.Should().Be(product1);
        result.Value.Items[0].AddedAt.Should().Be(time1);

        result.Value.Items[1].ProductId.Should().Be(product2);
        result.Value.Items[1].AddedAt.Should().Be(time2);
    }
}
