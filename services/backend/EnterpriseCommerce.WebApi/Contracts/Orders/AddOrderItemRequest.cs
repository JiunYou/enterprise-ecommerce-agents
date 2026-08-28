namespace EnterpriseCommerce.WebApi.Contracts.Orders;

public sealed record AddOrderItemRequest(
    Guid ProductId,
    int Quantity);
