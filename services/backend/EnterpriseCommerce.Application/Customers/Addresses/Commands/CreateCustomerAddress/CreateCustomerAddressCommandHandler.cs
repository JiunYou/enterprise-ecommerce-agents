using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Customers;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.CreateCustomerAddress;

public sealed class CreateCustomerAddressCommandHandler : ICommandHandler<CreateCustomerAddressCommand, CustomerAddressResponse>
{
    private readonly ICustomerAddressRepository _customerAddressRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateCustomerAddressCommandHandler(
        ICustomerAddressRepository customerAddressRepository,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _customerAddressRepository = customerAddressRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CustomerAddressResponse>> Handle(
        CreateCustomerAddressCommand request,
        CancellationToken cancellationToken)
    {
        var addressId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();

        var addressResult = CustomerAddress.Create(
            addressId,
            request.CustomerId,
            request.RecipientName,
            request.Phone,
            request.CountryCode,
            request.PostalCode,
            request.City,
            request.AddressLine1,
            request.AddressLine2,
            now);

        if (addressResult.IsFailure)
        {
            return Result.Failure<CustomerAddressResponse>(addressResult.Error);
        }

        var address = addressResult.Value;
        _customerAddressRepository.Add(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new CustomerAddressResponse(
            address.Id,
            address.RecipientName,
            address.Phone,
            address.CountryCode,
            address.PostalCode,
            address.City,
            address.AddressLine1,
            address.AddressLine2);

        return Result.Success(response);
    }
}
