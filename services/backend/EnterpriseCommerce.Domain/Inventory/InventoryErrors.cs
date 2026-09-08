using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Inventory;

public static class InventoryErrors
{
    public static readonly Error InsufficientStock = new(
        "Inventory.InsufficientStock", 
        "There is not enough available stock for this operation.");

    public static readonly Error InvalidReservationRelease = new(
        "Inventory.InvalidReservationRelease", 
        "Cannot release more stock than currently reserved.");

    public static readonly Error NegativeQuantity = new(
        "Inventory.NegativeQuantity", 
        "The operation requires a quantity greater than zero.");

    public static readonly Error NotFound = new(
        "Inventory.NotFound",
        "The inventory item for the specified product was not found.");

    public static readonly Error ConcurrencyConflict = new(
        "Inventory.ConcurrencyConflict",
        "The inventory item was modified by another operation.");
}
