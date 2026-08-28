using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Catalog;

public static class ProductErrors
{
    public static readonly Error NotFound = new(
        "Product.NotFound",
        "The product with the specified identifier was not found.");

    public static readonly Error InvalidPrice = new(
        "Product.InvalidPrice",
        "The product price must be greater than zero.");

    public static readonly Error AlreadyDeactivated = new(
        "Product.AlreadyDeactivated",
        "The product is already deactivated.");

    public static readonly Error NotActive = new(
        "Product.NotActive",
        "The product is not active and cannot be purchased.");
}
