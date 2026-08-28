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

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
