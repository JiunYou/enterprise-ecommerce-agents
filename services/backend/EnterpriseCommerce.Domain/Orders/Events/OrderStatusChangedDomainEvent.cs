using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Orders.Events;

public sealed record OrderStatusChangedDomainEvent(
    OrderId OrderId, 
    OrderStatus OldStatus, 
    OrderStatus NewStatus) : DomainEvent;
