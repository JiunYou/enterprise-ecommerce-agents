using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;

internal sealed class ResolveCustomerIdentityCommandHandler : ICommandHandler<ResolveCustomerIdentityCommand, Guid>
{
    private readonly ICustomerIdentityStore _customerIdentityStore;

    public ResolveCustomerIdentityCommandHandler(ICustomerIdentityStore customerIdentityStore)
    {
        _customerIdentityStore = customerIdentityStore;
    }

    public async Task<Result<Guid>> Handle(ResolveCustomerIdentityCommand request, CancellationToken cancellationToken)
    {
        var customerId = await _customerIdentityStore.ResolveOrCreateAsync(
            request.Issuer,
            request.Subject,
            cancellationToken);

        return Result.Success(customerId);
    }
}
