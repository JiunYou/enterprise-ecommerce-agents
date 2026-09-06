using EnterpriseCommerce.Domain.Orders.ValueObjects;

namespace EnterpriseCommerce.Infrastructure.Persistence.Orders;

public sealed class AdminOrderCancellation
{
    public OrderId OrderId { get; private set; } = null!;
    public string ActorIssuer { get; private set; } = null!;
    public string ActorSubject { get; private set; } = null!;
    public DateTimeOffset CancelledAt { get; private set; }
    public string Reason { get; private set; } = null!;

    private AdminOrderCancellation()
    {
    }

    public static AdminOrderCancellation Create(
        OrderId orderId,
        string actorIssuer,
        string actorSubject,
        DateTimeOffset cancelledAt,
        string reason)
    {
        return new AdminOrderCancellation
        {
            OrderId = orderId,
            ActorIssuer = actorIssuer,
            ActorSubject = actorSubject,
            CancelledAt = cancelledAt,
            Reason = reason
        };
    }
}
