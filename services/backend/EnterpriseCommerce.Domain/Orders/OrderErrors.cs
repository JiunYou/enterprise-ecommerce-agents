using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders;

public static class OrderErrors
{
    public static readonly Error EmptyOrder = new(
        "Order.EmptyOrder", 
        "Cannot perform operation on an empty order.");

    public static readonly Error InvalidQuantity = new(
        "Order.InvalidQuantity", 
        "The quantity must be greater than zero.");

    public static readonly Error InvalidStatusTransition = new(
        "Order.InvalidStatusTransition", 
        "The status transition is not allowed.");

    public static readonly Error ItemNotFound = new(
        "Order.ItemNotFound", 
        "The item was not found in the order.");

    public static readonly Error NotFound = new(
        "Order.NotFound", 
        "The order with the specified identifier was not found.");

    public static readonly Error CurrencyMismatch = new(
        "Order.CurrencyMismatch", 
        "Item currency must match order currency.");
}
