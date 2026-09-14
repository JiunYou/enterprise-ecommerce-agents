using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.UpdateCustomerAddress;

public sealed record UpdateCustomerAddressCommand(
    Guid CustomerId,
    Guid AddressId,
    string? RecipientName,
    string? Phone,
    string? CountryCode,
    string? PostalCode,
    string? City,
    string? AddressLine1,
    string? AddressLine2) : ICommand<CustomerAddressResponse>;
