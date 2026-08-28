using EnterpriseCommerce.Domain.Primitives;
using MediatR;

namespace EnterpriseCommerce.Application.Common.CQRS;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}
