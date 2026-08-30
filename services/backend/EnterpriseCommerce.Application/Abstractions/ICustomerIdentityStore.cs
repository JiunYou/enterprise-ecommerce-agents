namespace EnterpriseCommerce.Application.Abstractions;

public interface ICustomerIdentityStore
{
    Task<Guid> ResolveOrCreateAsync(string issuer, string subject, CancellationToken cancellationToken = default);
}
