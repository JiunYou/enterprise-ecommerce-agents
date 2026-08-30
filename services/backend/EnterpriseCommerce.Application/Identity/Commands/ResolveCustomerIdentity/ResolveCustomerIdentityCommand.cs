using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;

public sealed record ResolveCustomerIdentityCommand(string Issuer, string Subject) : ICommand<Guid>;
