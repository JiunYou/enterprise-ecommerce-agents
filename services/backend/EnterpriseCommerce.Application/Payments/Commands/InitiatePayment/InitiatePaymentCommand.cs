using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Payments;

namespace EnterpriseCommerce.Application.Payments.Commands.InitiatePayment;

public sealed record InitiatePaymentCommand(Guid OrderId, Guid IdempotencyKey, Guid CustomerId) : ICommand<InitiatePaymentResponse>;
