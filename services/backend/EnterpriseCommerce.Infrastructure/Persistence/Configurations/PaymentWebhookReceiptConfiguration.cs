using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class PaymentWebhookReceiptConfiguration : IEntityTypeConfiguration<PaymentWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookReceipt> builder)
    {
        builder.ToTable("PaymentWebhookReceipts");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.ProviderEventId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(r => r.PaymentAttemptId)
            .HasConversion(
                id => id == null ? (Guid?)null : id.Value,
                value => value.HasValue ? new PaymentAttemptId(value.Value) : null);

        builder.Property(r => r.ReceivedAt).IsRequired();

        // Webhook Idempotency
        builder.HasIndex(r => new { r.Provider, r.ProviderEventId })
            .IsUnique();
    }
}
