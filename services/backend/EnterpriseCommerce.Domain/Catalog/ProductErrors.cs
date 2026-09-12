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

    public static readonly Error AlreadyActive = new(
        "Product.AlreadyActive",
        "The product is already active.");

    public static readonly Error NotActive = new(
        "Product.NotActive",
        "The product is not active and cannot be purchased.");


    public static readonly Error ConcurrencyConflict = new(
        "Product.ConcurrencyConflict",
        "The product was modified by another operation.");

    public static readonly Error InvalidName = new(
        "Product.InvalidName",
        "The product name is invalid.");

    public static readonly Error InvalidDescription = new(
        "Product.InvalidDescription",
        "The product description is invalid.");
}
