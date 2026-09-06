using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class AdminOrderCancellationConfiguration : IEntityTypeConfiguration<AdminOrderCancellation>
{
    public void Configure(EntityTypeBuilder<AdminOrderCancellation> builder)
    {
        builder.ToTable("AdminOrderCancellations");

        builder.HasKey(x => x.OrderId);

        builder.Property(x => x.OrderId)
            .HasConversion(
                orderId => orderId.Value,
                value => new OrderId(value))
            .ValueGeneratedNever();

        builder.Property(x => x.ActorIssuer)
            .HasMaxLength(512)
            .UseCollation("ascii_bin")
            .IsRequired();

        builder.Property(x => x.ActorSubject)
            .HasMaxLength(255)
            .UseCollation("ascii_bin")
            .IsRequired();

        builder.Property(x => x.CancelledAt)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasOne<Order>()
            .WithOne()
            .HasForeignKey<AdminOrderCancellation>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
