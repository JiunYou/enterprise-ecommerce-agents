using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.DeleteCustomerAddress;

public sealed record DeleteCustomerAddressCommand(
    Guid CustomerId,
    Guid AddressId) : ICommand;
