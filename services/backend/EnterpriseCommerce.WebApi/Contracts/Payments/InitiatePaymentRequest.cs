namespace EnterpriseCommerce.WebApi.Contracts.Payments;

public record InitiatePaymentRequest(Guid OrderId, Guid IdempotencyKey);
