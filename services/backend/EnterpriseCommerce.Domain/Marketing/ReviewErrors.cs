using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Marketing;

public static class ReviewErrors
{
    public static readonly Error InvalidId = new(
        "Review.InvalidId",
        "The review identifier is invalid.");

    public static readonly Error InvalidCustomerId = new(
        "Review.InvalidCustomerId",
        "The customer identifier is invalid.");

    public static readonly Error InvalidProductId = new(
        "Review.InvalidProductId",
        "The product identifier is invalid.");

    public static readonly Error InvalidRating = new(
        "Review.InvalidRating",
        "The rating must be between 1 and 5.");

    public static readonly Error InvalidComment = new(
        "Review.InvalidComment",
        "The comment is required and must not exceed 2000 characters.");

    public static readonly Error AlreadyExists = new(
        "Review.AlreadyExists",
        "The customer has already reviewed this product.");

    public static readonly Error NotFound = new(
        "Review.NotFound",
        "The product review was not found.");
}
