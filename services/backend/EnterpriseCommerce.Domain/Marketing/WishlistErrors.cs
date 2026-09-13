using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Marketing;

public static class WishlistErrors
{
    public static readonly Error InvalidId = new(
        "Wishlist.InvalidId",
        "The wishlist item identifier is invalid.");

    public static readonly Error InvalidCustomerId = new(
        "Wishlist.InvalidCustomerId",
        "The customer identifier is invalid.");

    public static readonly Error InvalidProductId = new(
        "Wishlist.InvalidProductId",
        "The product identifier is invalid.");

    public static readonly Error AlreadyExists = new(
        "Wishlist.AlreadyExists",
        "The product is already in the wishlist.");

    public static readonly Error NotFound = new(
        "Wishlist.NotFound",
        "The wishlist item was not found.");
}
