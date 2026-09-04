namespace EnterpriseCommerce.WebApi.Contracts.Cart;

public sealed record AddCartItemRequest(Guid ProductId, int Quantity);
