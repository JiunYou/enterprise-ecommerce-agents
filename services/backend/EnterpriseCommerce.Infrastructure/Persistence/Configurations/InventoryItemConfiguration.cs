using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");

        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Version).IsConcurrencyToken();

        builder.Property(i => i.Id)
            .HasConversion(
                inventoryId => inventoryId.Value,
                value => new InventoryId(value));

        builder.Property(i => i.ProductReference)
            .HasConversion(
                productReference => productReference.Value,
                value => new ProductReference(value))
            .IsRequired();

        builder.Property(i => i.AvailableQuantity)
            .HasConversion(
                qty => qty.Value,
                value => new StockQuantity(value))
            .IsRequired();

        builder.Property(i => i.ReservedQuantity)
            .HasConversion(
                qty => qty.Value,
                value => new StockQuantity(value))
            .IsRequired();

        builder.OwnsMany(i => i.Reservations, rb =>
        {
            rb.ToTable("InventoryReservations");
            rb.HasKey(r => r.Id);

            rb.Property(r => r.Id)
                .HasConversion(
                    id => id.Value,
                    value => new InventoryReservationId(value));

            rb.Property(r => r.OrderReference)
                .HasConversion(
                    orderRef => orderRef.Value,
                    value => new OrderReference(value))
                .IsRequired();

            rb.Property(r => r.Quantity)
                .HasConversion(
                    qty => qty.Value,
                    value => new StockQuantity(value))
                .IsRequired();

            // Setup the foreign key back to InventoryItem
            rb.WithOwner().HasForeignKey("InventoryItemId");
        });
        
        // Tells EF to map the backing field for the collection
        builder.Metadata.FindNavigation(nameof(InventoryItem.Reservations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
