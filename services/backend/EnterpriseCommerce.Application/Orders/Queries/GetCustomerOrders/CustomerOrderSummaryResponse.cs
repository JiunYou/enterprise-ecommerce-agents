namespace EnterpriseCommerce.Application.Orders.Queries.GetCustomerOrders;

public sealed record CustomerOrderSummaryResponse(
    Guid Id,
    string Status,
    DateTimeOffset SubmittedAt,
    decimal TotalAmount,
    string Currency);
