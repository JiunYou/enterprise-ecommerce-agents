namespace EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

public sealed record OrderItemResponse(
    Guid ProductId,
    decimal UnitPrice,
    string Currency,
    int Quantity,
    decimal TotalPrice);

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    string Status,
    string Currency,
    decimal TotalAmount,
    IReadOnlyCollection<OrderItemResponse> Items);
