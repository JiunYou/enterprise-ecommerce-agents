using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.SetDefaultCustomerAddress;

public sealed record SetDefaultCustomerAddressCommand(
    Guid CustomerId,
    Guid AddressId) : ICommand<CustomerAddressResponse>;
