using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Reviews;
using EnterpriseCommerce.Application.Marketing.Reviews.Queries.GetProductReviews;
using EnterpriseCommerce.Domain.Catalog;
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

public class GetProductReviewsQueryHandlerTests
{
    private readonly Mock<IProductReviewRepository> _productReviewRepositoryMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly GetProductReviewsQueryHandler _handler;

    private readonly Guid _productId = Guid.NewGuid();

    public GetProductReviewsQueryHandlerTests()
    {
        _productReviewRepositoryMock = new Mock<IProductReviewRepository>();
        _productRepositoryMock = new Mock<IProductRepository>();

        _handler = new GetProductReviewsQueryHandler(
            _productReviewRepositoryMock.Object,
            _productRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenProductIsActive_ShouldReturnPagedReviewsSuccessfully()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-001", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var review1 = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 5, "第一則評論", DateTimeOffset.UtcNow).Value;
        var review2 = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 4, "第二則評論", DateTimeOffset.UtcNow.AddMinutes(-5)).Value;

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview> { review1, review2 }, 2, 4.5));

        var query = new GetProductReviewsQuery(_productId, Page: 1, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(2);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(10);

        result.Value.Items[0].Rating.Should().Be(5);
        result.Value.Items[0].Comment.Should().Be("第一則評論");
        result.Value.Items[1].Rating.Should().Be(4);
        result.Value.Items[1].Comment.Should().Be("第二則評論");
    }

    [Fact]
    public async Task Handle_WhenNoReviews_ShouldReturnNullAverageRating()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-NO-REVIEWS", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview>(), 0, (double?)null));

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
        result.Value.AverageRating.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSingleReview_ShouldReturnReviewRatingAsAverage()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-SINGLE-REVIEW", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var review = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 5, "單筆評論", DateTimeOffset.UtcNow).Value;

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview> { review }, 1, 5.0));

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.AverageRating.Should().Be(5.0);
    }

    [Fact]
    public async Task Handle_WhenMultipleReviews_ShouldReturnCorrectAverageRating()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-MULTI-REVIEW", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var r1 = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 5, "評論1", DateTimeOffset.UtcNow).Value;
        var r2 = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 4, "評論2", DateTimeOffset.UtcNow).Value;
        var r3 = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 3, "評論3", DateTimeOffset.UtcNow).Value;

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview> { r1, r2, r3 }, 3, 4.0));

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(3);
        result.Value.AverageRating.Should().Be(4.0);
    }

    [Fact]
    public async Task Handle_WhenPaged_AverageRatingShouldReflectAllReviewsIndependentOfCurrentPage()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-PAGED-REVIEW", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var r1 = ProductReview.Create(Guid.NewGuid(), Guid.NewGuid(), _productId, 5, "最新評論", DateTimeOffset.UtcNow).Value;

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview> { r1 }, 3, 4.0));

        var query = new GetProductReviewsQuery(_productId, Page: 1, PageSize: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.AverageRating.Should().Be(4.0);
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ShouldReturnNotFoundError()
    {
        // Arrange
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
        _productReviewRepositoryMock.Verify(r => r.GetPagedByProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductIsInactive_ShouldReturnNotFoundError()
    {
        // Arrange
        var product = Product.Create("Inactive Product", "SKU-002", 100m, "TWD").Value;
        product.Deactivate();

        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);
        _productReviewRepositoryMock.Verify(r => r.GetPagedByProductAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithDefaultPaginationParameters_ShouldUsePage1AndPageSize10()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-003", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview>(), 0, (double?)null));

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _productReviewRepositoryMock.Verify(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPageSizeExceedsMaximum50_ShouldClampTo50()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-004", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview>(), 0, (double?)null));

        var query = new GetProductReviewsQuery(_productId, Page: 1, PageSize: 100);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _productReviewRepositoryMock.Verify(r => r.GetPagedByProductAsync(_productId, 1, 50, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPageIsLessThan1_ShouldDefaultToPage1()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-005", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview>(), 0, (double?)null));

        var query = new GetProductReviewsQuery(_productId, Page: 0, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _productReviewRepositoryMock.Verify(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOutOfRangePage_ShouldReturnEmptyItemsWithAccurateTotalCount()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-006", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 999, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview>(), 5, 4.2));

        var query = new GetProductReviewsQuery(_productId, Page: 999, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(5);
        result.Value.Page.Should().Be(999);
    }

    [Fact]
    public async Task Handle_PublicReviewResponse_MustNotExposeCustomerIdOrInternalId()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-007", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var secretCustomerId = Guid.NewGuid();
        var internalReviewId = Guid.NewGuid();
        var review = ProductReview.Create(internalReviewId, secretCustomerId, _productId, 5, "隱私測試", DateTimeOffset.UtcNow).Value;

        _productReviewRepositoryMock
            .Setup(r => r.GetPagedByProductAsync(_productId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<ProductReview> { review }, 1, 5.0));

        var query = new GetProductReviewsQuery(_productId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Single();

        // 透過反射確認公開 DTO 屬性名稱絕無包含 CustomerId 或 Id
        var properties = typeof(ProductReviewItemResponse).GetProperties().Select(p => p.Name).ToList();
        properties.Should().NotContain("CustomerId");
        properties.Should().NotContain("Id");
        properties.Should().NotContain("ReviewId");
        properties.Should().Contain(new[] { "Rating", "Comment", "CreatedAt" });
    }
}
