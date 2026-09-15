using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Customers;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.SetDefaultCustomerAddress;

public sealed class SetDefaultCustomerAddressCommandHandler : ICommandHandler<SetDefaultCustomerAddressCommand, CustomerAddressResponse>
{
    private readonly ICustomerAddressRepository _customerAddressRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public SetDefaultCustomerAddressCommandHandler(
        ICustomerAddressRepository customerAddressRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _customerAddressRepository = customerAddressRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CustomerAddressResponse>> Handle(
        SetDefaultCustomerAddressCommand request,
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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var addresses = await _customerAddressRepository.GetByCustomerForUpdateAsync(
                request.CustomerId,
                cancellationToken);

            var targetAddress = addresses.FirstOrDefault(a => a.Id == request.AddressId);
            if (targetAddress is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.NotFound);
            }

            foreach (var address in addresses)
            {
                if (address.Id == targetAddress.Id)
                {
                    address.SetAsDefault();
                }
                else
                {
                    address.ClearDefault();
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            var response = new CustomerAddressResponse(
                targetAddress.Id,
                targetAddress.RecipientName,
                targetAddress.Phone,
                targetAddress.CountryCode,
                targetAddress.PostalCode,
                targetAddress.City,
                targetAddress.AddressLine1,
                targetAddress.AddressLine2,
                targetAddress.IsDefault);

            return Result.Success(response);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
