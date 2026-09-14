namespace EnterpriseCommerce.Application.Orders.Queries.GetCart;

public sealed record CartItemResponse(
    Guid ProductId,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal TotalPrice);

public sealed record CartResponse(
    Guid? Id,
    string Currency,
    decimal TotalAmount,
    IReadOnlyCollection<CartItemResponse> Items,
    decimal SubtotalAmount = 0m,
    decimal DiscountAmount = 0m,
    string? AppliedCouponCode = null)
{
    public static CartResponse Empty(string currency = "USD") =>
        new(null, currency, 0m, Array.Empty<CartItemResponse>(), 0m, 0m, null);
}
