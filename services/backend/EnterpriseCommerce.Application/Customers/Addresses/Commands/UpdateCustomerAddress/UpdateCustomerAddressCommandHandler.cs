using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Customers;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.UpdateCustomerAddress;

public sealed class UpdateCustomerAddressCommandHandler : ICommandHandler<UpdateCustomerAddressCommand, CustomerAddressResponse>
{
    private readonly ICustomerAddressRepository _customerAddressRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public UpdateCustomerAddressCommandHandler(
        ICustomerAddressRepository customerAddressRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _customerAddressRepository = customerAddressRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CustomerAddressResponse>> Handle(
        UpdateCustomerAddressCommand request,
        CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.InvalidCustomerId);
        }

        if (request.AddressId == Guid.Empty)
        {
            return Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.InvalidId);
        }

        var address = await _customerAddressRepository.GetByIdForCustomerAsync(
            request.CustomerId,
            request.AddressId,
            cancellationToken);

        if (address is null)
        {
            return Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.NotFound);
        }

        var updateResult = address.Update(
            request.RecipientName,
            request.Phone,
            request.CountryCode,
            request.PostalCode,
            request.City,
            request.AddressLine1,
            request.AddressLine2);

        if (updateResult.IsFailure)
        {
            return Result.Failure<CustomerAddressResponse>(updateResult.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new CustomerAddressResponse(
            address.Id,
            address.RecipientName,
            address.Phone,
            address.CountryCode,
            address.PostalCode,
            address.City,
            address.AddressLine1,
            address.AddressLine2,
            address.IsDefault);

        return Result.Success(response);
    }
}
