using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Infrastructure.Persistence.Customers;

public sealed class CustomerAddressRepository : ICustomerAddressRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public CustomerAddressRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(CustomerAddress address)
    {
        _dbContext.CustomerAddresses.Add(address);
    }

    public async Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerAddresses
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerAddress?> GetByIdForCustomerAsync(
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.CustomerAddresses
            .FirstOrDefaultAsync(a => a.CustomerId == customerId && a.Id == addressId, cancellationToken);
    }

    public void Remove(CustomerAddress address)
    {
        _dbContext.CustomerAddresses.Remove(address);
    }
}
