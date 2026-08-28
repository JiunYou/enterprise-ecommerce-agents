using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory;

public sealed class InventoryReservation : Entity<InventoryReservationId>
{
    public OrderReference OrderReference { get; private set; } = default!;
    public StockQuantity Quantity { get; private set; } = StockQuantity.Zero;
    public DateTimeOffset CreatedAt { get; private set; }

    internal InventoryReservation(InventoryReservationId id, OrderReference orderReference, StockQuantity quantity, DateTimeOffset createdAt) 
        : base(id)
    {
        OrderReference = orderReference;
        Quantity = quantity;
        CreatedAt = createdAt;
    }

    private InventoryReservation()
    {
    }
}
