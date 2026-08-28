using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class PaymentAttemptConfiguration : IEntityTypeConfiguration<PaymentAttempt>
{
    public void Configure(EntityTypeBuilder<PaymentAttempt> builder)
    {
        builder.ToTable("PaymentAttempts");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Version).IsConcurrencyToken();

        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PaymentAttemptId(value));

        builder.Property(p => p.OrderId)
            .HasConversion(
                id => id.Value,
                value => new OrderId(value))
            .IsRequired();

        builder.OwnsOne(p => p.Amount, amountBuilder =>
        {
            amountBuilder.Property(m => m.Amount)
                .HasColumnName("Amount")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            amountBuilder.Property(m => m.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(p => p.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.ProviderTransactionId)
            .HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.IdempotencyKey)
            .IsRequired();

        builder.Property(p => p.CreatedAt).IsRequired();
        
        builder.Property(p => p.CompletedAt);

        // Initiation Idempotency
        builder.HasIndex(p => new { p.OrderId, p.IdempotencyKey })
            .IsUnique();

        // Provider Transaction Idempotency
        // ProviderTransactionId is nullable, so we apply unique constraint where it is not null
        builder.HasIndex(p => new { p.Provider, p.ProviderTransactionId })
            .IsUnique();

        // For indexing purposes
        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.ProviderTransactionId);
    }
}
