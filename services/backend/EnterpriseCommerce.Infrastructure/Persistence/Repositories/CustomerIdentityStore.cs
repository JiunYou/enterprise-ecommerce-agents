using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class CustomerIdentityStore : ICustomerIdentityStore
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public CustomerIdentityStore(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> ResolveOrCreateAsync(
        string issuer,
        string subject,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.CustomerIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject, cancellationToken);

        if (existing != null)
        {
            return existing.Id;
        }

        var newId = Guid.NewGuid();
        var identity = CustomerIdentity.Create(newId, issuer, subject, DateTimeOffset.UtcNow);

        _dbContext.CustomerIdentities.Add(identity);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return newId;
        }
        catch (DbUpdateException ex)
        {
            if (!IsDuplicateKeyException(ex))
            {
                throw;
            }

            _dbContext.Entry(identity).State = EntityState.Detached;

            var resolved = await _dbContext.CustomerIdentities
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Issuer == issuer && x.Subject == subject, cancellationToken);

            if (resolved != null)
            {
                return resolved.Id;
            }

            throw;
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var current = (Exception?)ex;
        while (current != null)
        {
            if (current is MySqlConnector.MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
