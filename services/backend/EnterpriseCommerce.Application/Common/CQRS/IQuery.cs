using EnterpriseCommerce.Domain.Primitives;
using MediatR;

namespace EnterpriseCommerce.Application.Common.CQRS;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
