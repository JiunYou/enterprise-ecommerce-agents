using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Marketing.Reviews;
using EnterpriseCommerce.Application.Marketing.Reviews.Commands.CreateProductReview;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Marketing.Commands;

public class CreateProductReviewCommandHandlerTests
{
    private readonly Mock<IProductReviewRepository> _productReviewRepositoryMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CreateProductReviewCommandHandler _handler;

    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _productId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    public CreateProductReviewCommandHandlerTests()
    {
        _productReviewRepositoryMock = new Mock<IProductReviewRepository>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _handler = new CreateProductReviewCommandHandler(
            _productReviewRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WhenProductIsActiveAndNoPriorReview_ShouldCreateReviewAndSaveChanges()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-ACTIVE-001", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.ExistsAsync(_customerId, _productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateProductReviewCommand(_customerId, _productId, 5, "很讚的商品！");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _productReviewRepositoryMock.Verify(r => r.Add(It.Is<ProductReview>(review =>
            review.CustomerId == _customerId &&
            review.ProductId == _productId &&
            review.Rating == 5 &&
            review.Comment == "很讚的商品！" &&
            review.CreatedAt == _fixedTime)), Times.Once);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenProductDoesNotExist_ShouldReturnNotFoundErrorAndNotSaveChanges()
    {
        // Arrange
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var command = new CreateProductReviewCommand(_customerId, _productId, 5, "Good product");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);

        _productReviewRepositoryMock.Verify(r => r.Add(It.IsAny<ProductReview>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductIsInactive_ShouldReturnNotFoundErrorAndNotSaveChanges()
    {
        // Arrange
        var product = Product.Create("Inactive Product", "SKU-INACTIVE-001", 100m, "TWD").Value;
        product.Deactivate();

        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = new CreateProductReviewCommand(_customerId, _productId, 5, "Good product");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductErrors.NotFound);

        _productReviewRepositoryMock.Verify(r => r.Add(It.IsAny<ProductReview>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCustomerAlreadyReviewedProduct_ShouldReturnAlreadyExistsErrorAndNotSaveChanges()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-ACTIVE-002", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.ExistsAsync(_customerId, _productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateProductReviewCommand(_customerId, _productId, 4, "重複評論");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.AlreadyExists);

        _productReviewRepositoryMock.Verify(r => r.Add(It.IsAny<ProductReview>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Handle_WhenRatingIsInvalid_ShouldReturnInvalidRatingError(int invalidRating)
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-ACTIVE-003", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.ExistsAsync(_customerId, _productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateProductReviewCommand(_customerId, _productId, invalidRating, "Valid comment");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidRating);

        _productReviewRepositoryMock.Verify(r => r.Add(It.IsAny<ProductReview>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenCommentIsInvalid_ShouldReturnInvalidCommentError(string? invalidComment)
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-ACTIVE-004", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.ExistsAsync(_customerId, _productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateProductReviewCommand(_customerId, _productId, 5, invalidComment!);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidComment);

        _productReviewRepositoryMock.Verify(r => r.Add(It.IsAny<ProductReview>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCommentExceeds2000Characters_ShouldReturnInvalidCommentError()
    {
        // Arrange
        var product = Product.Create("Active Product", "SKU-ACTIVE-005", 100m, "TWD").Value;
        _productRepositoryMock
            .Setup(r => r.GetByIdAsync(_productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _productReviewRepositoryMock
            .Setup(r => r.ExistsAsync(_customerId, _productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var longComment = new string('X', 2001);
        var command = new CreateProductReviewCommand(_customerId, _productId, 5, longComment);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidComment);

        _productReviewRepositoryMock.Verify(r => r.Add(It.IsAny<ProductReview>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
