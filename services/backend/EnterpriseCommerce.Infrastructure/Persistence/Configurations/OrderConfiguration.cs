using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);
        
        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.Property(o => o.Id)
            .HasConversion(
                orderId => orderId.Value,
                value => new OrderId(value));

        builder.Property(o => o.CustomerId).IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(o => o.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(o => o.SubmittedAt);

        builder.HasIndex(o => new { o.Status, o.SubmittedAt });

        builder.OwnsOne(o => o.ShippingAddress, shippingBuilder =>
        {
            shippingBuilder.Property(s => s.RecipientName)
                .HasColumnName("ShippingRecipientName")
                .HasMaxLength(100);

            shippingBuilder.Property(s => s.Phone)
                .HasColumnName("ShippingPhone")
                .HasMaxLength(30);

            shippingBuilder.Property(s => s.CountryCode)
                .HasColumnName("ShippingCountryCode")
                .HasMaxLength(2);

            shippingBuilder.Property(s => s.PostalCode)
                .HasColumnName("ShippingPostalCode")
                .HasMaxLength(20);

            shippingBuilder.Property(s => s.City)
                .HasColumnName("ShippingCity")
                .HasMaxLength(100);

            shippingBuilder.Property(s => s.AddressLine1)
                .HasColumnName("ShippingAddressLine1")
                .HasMaxLength(200);

            shippingBuilder.Property(s => s.AddressLine2)
                .HasColumnName("ShippingAddressLine2")
                .HasMaxLength(200);
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
