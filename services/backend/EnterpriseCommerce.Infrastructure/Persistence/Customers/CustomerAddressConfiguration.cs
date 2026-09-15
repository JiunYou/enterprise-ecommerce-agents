using EnterpriseCommerce.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseCommerce.Infrastructure.Persistence.Customers;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.CustomerId)
            .IsRequired();

        builder.Property(a => a.RecipientName)
            .HasMaxLength(CustomerAddress.MaxRecipientNameLength)
            .IsRequired();

        builder.Property(a => a.Phone)
            .HasMaxLength(CustomerAddress.MaxPhoneLength)
            .IsRequired();

        builder.Property(a => a.CountryCode)
            .HasMaxLength(CustomerAddress.CountryCodeLength)
            .IsRequired();

        builder.Property(a => a.PostalCode)
            .HasMaxLength(CustomerAddress.MaxPostalCodeLength)
            .IsRequired();

        builder.Property(a => a.City)
            .HasMaxLength(CustomerAddress.MaxCityLength)
            .IsRequired();

        builder.Property(a => a.AddressLine1)
            .HasMaxLength(CustomerAddress.MaxAddressLineLength)
            .IsRequired();

        builder.Property(a => a.AddressLine2)
            .HasMaxLength(CustomerAddress.MaxAddressLineLength)
            .IsRequired(false);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(a => a.CustomerId);
    }
}
