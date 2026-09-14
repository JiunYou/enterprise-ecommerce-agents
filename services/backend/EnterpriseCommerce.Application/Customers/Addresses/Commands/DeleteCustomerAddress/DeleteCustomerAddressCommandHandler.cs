using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Customers;
using EnterpriseCommerce.Domain.Primitives;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Customers.Addresses.Commands.DeleteCustomerAddress;

public sealed class DeleteCustomerAddressCommandHandler : ICommandHandler<DeleteCustomerAddressCommand>
{
    private readonly ICustomerAddressRepository _customerAddressRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;

    public DeleteCustomerAddressCommandHandler(
        ICustomerAddressRepository customerAddressRepository,
        IApplicationUnitOfWork unitOfWork)
    {
        _customerAddressRepository = customerAddressRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        DeleteCustomerAddressCommand request,
        CancellationToken cancellationToken)
    {
        var address = await _customerAddressRepository.GetByIdForCustomerAsync(
            request.CustomerId,
            request.AddressId,
            cancellationToken);

        if (address is null)
        {
            return Result.Failure(CustomerAddressErrors.NotFound);
        }

        _customerAddressRepository.Remove(address);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
