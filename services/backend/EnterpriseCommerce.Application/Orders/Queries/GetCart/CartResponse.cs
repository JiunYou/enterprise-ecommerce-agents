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
    IReadOnlyCollection<CartItemResponse> Items)
{
    public static CartResponse Empty(string currency = "USD") =>
        new(null, currency, 0m, Array.Empty<CartItemResponse>());
}
