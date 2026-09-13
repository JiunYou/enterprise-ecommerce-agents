using EnterpriseCommerce.Domain.Primitives;
using System;

namespace EnterpriseCommerce.Domain.Marketing;

/// <summary>
/// 顧客商品評論領域實體 (Marketing Context)
/// </summary>
public sealed class ProductReview : Entity<Guid>
{
    private ProductReview(
        Guid id,
        Guid customerId,
        Guid productId,
        int rating,
        string comment,
        DateTimeOffset createdAt)
        : base(id)
    {
        CustomerId = customerId;
        ProductId = productId;
        Rating = rating;
        Comment = comment;
        CreatedAt = createdAt;
    }

    private ProductReview()
    {
    }

    public Guid CustomerId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Rating { get; private set; }
    public string Comment { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// 建立顧客商品評論工廠方法
    /// </summary>
    public static Result<ProductReview> Create(
        Guid id,
        Guid customerId,
        Guid productId,
        int rating,
        string comment,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<ProductReview>(ReviewErrors.InvalidId);
        }

        if (customerId == Guid.Empty)
        {
            return Result.Failure<ProductReview>(ReviewErrors.InvalidCustomerId);
        }

        if (productId == Guid.Empty)
        {
            return Result.Failure<ProductReview>(ReviewErrors.InvalidProductId);
        }

        if (rating < 1 || rating > 5)
        {
            return Result.Failure<ProductReview>(ReviewErrors.InvalidRating);
        }

        if (comment is null)
        {
            return Result.Failure<ProductReview>(ReviewErrors.InvalidComment);
        }

        var normalizedComment = comment.Trim();

        if (string.IsNullOrWhiteSpace(normalizedComment) || normalizedComment.Length > 2000)
        {
            return Result.Failure<ProductReview>(ReviewErrors.InvalidComment);
        }

        return Result.Success(new ProductReview(
            id,
            customerId,
            productId,
            rating,
            normalizedComment,
            createdAt));
    }
}
