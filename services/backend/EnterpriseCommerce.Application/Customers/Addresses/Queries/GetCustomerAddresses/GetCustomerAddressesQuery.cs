using EnterpriseCommerce.Application.Common.CQRS;
using System;
using System.Collections.Generic;

namespace EnterpriseCommerce.Application.Customers.Addresses.Queries.GetCustomerAddresses;

public sealed record GetCustomerAddressesQuery(
    Guid CustomerId) : IQuery<IReadOnlyList<CustomerAddressResponse>>;
