using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders.Events;

public sealed record OrderCreatedDomainEvent(
    OrderId OrderId, 
    Guid CustomerId) : DomainEvent;
