using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory.Events;

public sealed record InventoryItemCreatedDomainEvent(
    InventoryId InventoryId, 
    ProductReference ProductReference) : DomainEvent;

public sealed record StockIncreasedDomainEvent(
    InventoryId InventoryId, 
    StockQuantity AddedQuantity, 
    StockQuantity NewAvailableQuantity) : DomainEvent;

public sealed record StockDecreasedDomainEvent(
    InventoryId InventoryId, 
    StockQuantity RemovedQuantity, 
    StockQuantity NewAvailableQuantity) : DomainEvent;

public sealed record StockReservedDomainEvent(
    InventoryId InventoryId, 
    OrderReference OrderReference,
    StockQuantity ReservedQuantity,
    StockQuantity NewAvailableQuantity,
    StockQuantity TotalReservedQuantity) : DomainEvent;

public sealed record StockReservationReleasedDomainEvent(
    InventoryId InventoryId, 
    OrderReference OrderReference,
    StockQuantity ReleasedQuantity,
    StockQuantity NewAvailableQuantity,
    StockQuantity TotalReservedQuantity) : DomainEvent;
