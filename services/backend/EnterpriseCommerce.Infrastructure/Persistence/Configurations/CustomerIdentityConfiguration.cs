using EnterpriseCommerce.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Configurations;

internal sealed class CustomerIdentityConfiguration : IEntityTypeConfiguration<CustomerIdentity>
{
    public void Configure(EntityTypeBuilder<CustomerIdentity> builder)
    {
        builder.ToTable("CustomerIdentities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Issuer)
            .HasMaxLength(512)
            .UseCollation("ascii_bin")
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasMaxLength(255)
            .UseCollation("ascii_bin")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.Issuer, x.Subject })
            .IsUnique();
    }
}
