using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class PaymentRefundConfiguration : IEntityTypeConfiguration<PaymentRefund>
{
    public void Configure(EntityTypeBuilder<PaymentRefund> builder)
    {
        builder.ToTable("PaymentRefunds");

        // Primary key & FK to PaymentAttempt (Id == PaymentAttemptId, no separate PK generated)
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("PaymentAttemptId")
            .HasConversion(
                id => id.Value,
                value => new PaymentAttemptId(value))
            .ValueGeneratedNever();

        builder.HasOne<PaymentAttempt>()
            .WithOne()
            .HasForeignKey<PaymentRefund>(p => p.Id)
            .OnDelete(DeleteBehavior.Restrict);

        // Columns
        builder.Property(p => p.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(p => p.Reason)
            .HasMaxLength(500)
            .IsUnicode(true)
            .IsRequired();

        builder.Property(p => p.ActorIssuer)
            .HasMaxLength(512)
            .IsUnicode(false)
            .UseCollation("ascii_bin")
            .IsRequired();

        builder.Property(p => p.ActorSubject)
            .HasMaxLength(255)
            .IsUnicode(false)
            .UseCollation("ascii_bin")
            .IsRequired();

        builder.Property(p => p.RequestedAt)
            .IsRequired();

        builder.Property(p => p.CompletedAt);

        // Concurrency token (uint, not row-version binary)
        builder.Property(p => p.Version)
            .IsConcurrencyToken();
    }
}
