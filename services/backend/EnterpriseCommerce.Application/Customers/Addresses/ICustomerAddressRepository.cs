using EnterpriseCommerce.Domain.Customers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Customers.Addresses;

public interface ICustomerAddressRepository
{
    void Add(CustomerAddress address);

    Task<IReadOnlyList<CustomerAddress>> GetByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerAddress>> GetByCustomerForUpdateAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<CustomerAddress?> GetByIdForCustomerAsync(
        Guid customerId,
        Guid addressId,
        CancellationToken cancellationToken = default);

    void Remove(CustomerAddress address);
}
