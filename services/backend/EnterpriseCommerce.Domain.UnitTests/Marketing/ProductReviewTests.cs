using EnterpriseCommerce.Domain.Marketing;
using FluentAssertions;
using System;
using Xunit;

namespace EnterpriseCommerce.Domain.UnitTests.Marketing;

public class ProductReviewTests
{
    private readonly Guid _validId = Guid.NewGuid();
    private readonly Guid _validCustomerId = Guid.NewGuid();
    private readonly Guid _validProductId = Guid.NewGuid();
    private readonly DateTimeOffset _validCreatedAt = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_WithValidParameters_ShouldReturnSuccess()
    {
        // Arrange
        const int rating = 5;
        const string comment = "這是一個很棒的商品！";

        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            rating,
            comment,
            _validCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var review = result.Value;
        review.Id.Should().Be(_validId);
        review.CustomerId.Should().Be(_validCustomerId);
        review.ProductId.Should().Be(_validProductId);
        review.Rating.Should().Be(rating);
        review.Comment.Should().Be(comment);
        review.CreatedAt.Should().Be(_validCreatedAt);
    }

    [Fact]
    public void Create_WithEmptyId_ShouldReturnInvalidIdError()
    {
        // Act
        var result = ProductReview.Create(
            Guid.Empty,
            _validCustomerId,
            _validProductId,
            5,
            "Good product",
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidId);
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldReturnInvalidCustomerIdError()
    {
        // Act
        var result = ProductReview.Create(
            _validId,
            Guid.Empty,
            _validProductId,
            5,
            "Good product",
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidCustomerId);
    }

    [Fact]
    public void Create_WithEmptyProductId_ShouldReturnInvalidProductIdError()
    {
        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            Guid.Empty,
            5,
            "Good product",
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidProductId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(10)]
    public void Create_WithInvalidRating_ShouldReturnInvalidRatingError(int invalidRating)
    {
        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            invalidRating,
            "Good product",
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidRating);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Create_WithBoundaryRatings_ShouldReturnSuccess(int boundaryRating)
    {
        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            boundaryRating,
            "Good product",
            _validCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(boundaryRating);
    }

    [Fact]
    public void Create_WithNullComment_ShouldReturnInvalidCommentError()
    {
        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            5,
            null!,
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidComment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Create_WithWhitespaceComment_ShouldReturnInvalidCommentError(string whitespaceComment)
    {
        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            5,
            whitespaceComment,
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidComment);
    }

    [Fact]
    public void Create_ShouldTrimCommentWhitespace()
    {
        // Arrange
        const string rawComment = "   前後有空白的留言   ";

        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            5,
            rawComment,
            _validCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Comment.Should().Be("前後有空白的留言");
    }

    [Fact]
    public void Create_WithExactly2000CharactersComment_ShouldReturnSuccess()
    {
        // Arrange
        var comment2000 = new string('A', 2000);

        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            5,
            comment2000,
            _validCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Comment.Length.Should().Be(2000);
    }

    [Fact]
    public void Create_WithMoreThan2000CharactersComment_ShouldReturnInvalidCommentError()
    {
        // Arrange
        var comment2001 = new string('A', 2001);

        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            5,
            comment2001,
            _validCreatedAt);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ReviewErrors.InvalidComment);
    }

    [Fact]
    public void Create_WithInternalNewlines_ShouldPreserveInternalNewlines()
    {
        // Arrange
        const string multilineComment = "第一行評論\n第二行評論\r\n第三行評論";

        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            5,
            multilineComment,
            _validCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Comment.Should().Be(multilineComment);
    }

    [Fact]
    public void Create_ShouldPreserveSuppliedCreatedAt()
    {
        // Arrange
        var customCreatedAt = new DateTimeOffset(2026, 9, 13, 12, 34, 56, TimeSpan.Zero);

        // Act
        var result = ProductReview.Create(
            _validId,
            _validCustomerId,
            _validProductId,
            4,
            "測試時間保留",
            customCreatedAt);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedAt.Should().Be(customCreatedAt);
    }
}
