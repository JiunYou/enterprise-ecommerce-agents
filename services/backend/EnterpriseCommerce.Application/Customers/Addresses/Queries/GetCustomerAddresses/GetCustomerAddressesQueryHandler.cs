using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Customers.Addresses.Queries.GetCustomerAddresses;

public sealed class GetCustomerAddressesQueryHandler : IQueryHandler<GetCustomerAddressesQuery, IReadOnlyList<CustomerAddressResponse>>
{
    private readonly ICustomerAddressRepository _customerAddressRepository;

    public GetCustomerAddressesQueryHandler(ICustomerAddressRepository customerAddressRepository)
    {
        _customerAddressRepository = customerAddressRepository;
    }

    public async Task<Result<IReadOnlyList<CustomerAddressResponse>>> Handle(
        GetCustomerAddressesQuery request,
        CancellationToken cancellationToken)
    {
        var addresses = await _customerAddressRepository.GetByCustomerAsync(
            request.CustomerId,
            cancellationToken);

        var response = addresses.Select(a => new CustomerAddressResponse(
            a.Id,
            a.RecipientName,
            a.Phone,
            a.CountryCode,
            a.PostalCode,
            a.City,
            a.AddressLine1,
            a.AddressLine2)).ToList();

        return Result.Success<IReadOnlyList<CustomerAddressResponse>>(response);
    }
}
