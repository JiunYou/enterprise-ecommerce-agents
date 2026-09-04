using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Orders.Commands.SubmitOrder;

public sealed record SubmitOrderCommand(
    Guid OrderId,
    Guid CustomerId,
    ShippingAddressDto ShippingAddress) : ICommand;
