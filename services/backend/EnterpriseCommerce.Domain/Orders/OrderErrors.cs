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

    public static readonly Error ShippingAddressRequired = new(
        "Order.ShippingAddressRequired",
        "A valid shipping address is required to submit the order.");

    public static readonly Error InvalidShippingRecipientName = new(
        "Order.InvalidShippingRecipientName",
        "Recipient name is required and must not exceed 100 characters.");

    public static readonly Error InvalidShippingPhone = new(
        "Order.InvalidShippingPhone",
        "Phone number is required, must not contain control characters, and must not exceed 30 characters.");

    public static readonly Error InvalidShippingCountryCode = new(
        "Order.InvalidShippingCountryCode",
        "Country code is required and must consist of exactly 2 letters.");

    public static readonly Error InvalidShippingPostalCode = new(
        "Order.InvalidShippingPostalCode",
        "Postal code is required and must not exceed 20 characters.");

    public static readonly Error InvalidShippingCity = new(
        "Order.InvalidShippingCity",
        "City is required and must not exceed 100 characters.");

    public static readonly Error InvalidShippingAddressLine1 = new(
        "Order.InvalidShippingAddressLine1",
        "Address line 1 is required and must not exceed 200 characters.");

    public static readonly Error InvalidShippingAddressLine2 = new(
        "Order.InvalidShippingAddressLine2",
        "Address line 2 must not exceed 200 characters.");
}
