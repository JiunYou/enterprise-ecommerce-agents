using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.OrderId)
            .HasConversion(
                orderId => orderId.Value,
                value => new OrderId(value))
            .IsRequired();

        builder.Property(i => i.ProductId)
            .HasConversion(
                productId => productId.Value,
                value => new ProductId(value))
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.OwnsOne(i => i.UnitPrice, priceBuilder =>
        {
            priceBuilder.Property(m => m.Amount)
                .HasColumnName("UnitPriceAmount")
                .HasPrecision(18, 4)
                .IsRequired();

            priceBuilder.Property(m => m.Currency)
                .HasColumnName("UnitPriceCurrency")
                .HasMaxLength(3)
                .IsRequired();
        });
    }
}
