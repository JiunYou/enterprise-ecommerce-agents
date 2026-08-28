using EnterpriseCommerce.Domain.Inventory.Events;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory;

public sealed class InventoryItem : AggregateRoot<InventoryId>
{
    private readonly List<InventoryReservation> _reservations = [];

    public ProductReference ProductReference { get; private set; } = default!;
    public StockQuantity AvailableQuantity { get; private set; } = StockQuantity.Zero;
    public StockQuantity ReservedQuantity { get; private set; } = StockQuantity.Zero;
    public IReadOnlyCollection<InventoryReservation> Reservations => _reservations.AsReadOnly();

    private InventoryItem(InventoryId id, ProductReference productReference) : base(id)
    {
        ProductReference = productReference;
    }

    private InventoryItem()
    {
    }

    public static InventoryItem Create(ProductReference productReference)
    {
        var item = new InventoryItem(new InventoryId(Guid.NewGuid()), productReference);
        item.RaiseDomainEvent(new InventoryItemCreatedDomainEvent(item.Id, productReference));
        return item;
    }

    public Result IncreaseStock(StockQuantity quantity)
    {
        if (quantity.Value <= 0)
        {
            return Result.Failure(InventoryErrors.NegativeQuantity);
        }

        AvailableQuantity += quantity;
        RaiseDomainEvent(new StockIncreasedDomainEvent(Id, quantity, AvailableQuantity));

        return Result.Success();
    }

    public Result DecreaseStock(StockQuantity quantity)
    {
        if (quantity.Value <= 0)
        {
            return Result.Failure(InventoryErrors.NegativeQuantity);
        }

        if (AvailableQuantity < quantity)
        {
            return Result.Failure(InventoryErrors.InsufficientStock);
        }

        AvailableQuantity -= quantity;
        RaiseDomainEvent(new StockDecreasedDomainEvent(Id, quantity, AvailableQuantity));

        return Result.Success();
    }

    public Result ReserveStock(OrderReference orderReference, StockQuantity quantity)
    {
        if (quantity.Value <= 0)
        {
            return Result.Failure(InventoryErrors.NegativeQuantity);
        }

        if (_reservations.Any(r => r.OrderReference == orderReference))
        {
            // Idempotent: already reserved for this order
            return Result.Success();
        }

        if (AvailableQuantity < quantity)
        {
            return Result.Failure(InventoryErrors.InsufficientStock);
        }

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;

        var reservation = new InventoryReservation(new InventoryReservationId(Guid.NewGuid()), orderReference, quantity, DateTimeOffset.UtcNow);
        _reservations.Add(reservation);

        RaiseDomainEvent(new StockReservedDomainEvent(Id, orderReference, quantity, AvailableQuantity, ReservedQuantity));

        return Result.Success();
    }

    public Result ReleaseReservation(OrderReference orderReference)
    {
        var reservation = _reservations.SingleOrDefault(r => r.OrderReference == orderReference);
        if (reservation is null)
        {
            // Idempotent: either already released or never existed
            return Result.Success();
        }

        ReservedQuantity -= reservation.Quantity;
        AvailableQuantity += reservation.Quantity;
        _reservations.Remove(reservation);

        RaiseDomainEvent(new StockReservationReleasedDomainEvent(Id, orderReference, reservation.Quantity, AvailableQuantity, ReservedQuantity));

        return Result.Success();
    }
}
