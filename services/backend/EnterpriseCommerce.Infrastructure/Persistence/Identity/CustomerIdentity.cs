namespace EnterpriseCommerce.Infrastructure.Persistence.Identity;

public sealed class CustomerIdentity
{
    public Guid Id { get; private set; }
    public string Issuer { get; private set; } = null!;
    public string Subject { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private CustomerIdentity()
    {
    }

    public static CustomerIdentity Create(Guid customerId, string issuer, string subject, DateTimeOffset createdAt)
    {
        return new CustomerIdentity
        {
            Id = customerId,
            Issuer = issuer,
            Subject = subject,
            CreatedAt = createdAt
        };
    }
}
