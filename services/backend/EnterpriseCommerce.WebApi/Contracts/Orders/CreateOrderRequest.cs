namespace EnterpriseCommerce.WebApi.Contracts.Orders;

public sealed record CreateOrderRequest(Guid CustomerId, string Currency);
