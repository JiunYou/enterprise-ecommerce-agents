using EnterpriseCommerce.Domain.Primitives;
using MediatR;

namespace EnterpriseCommerce.Application.Common.CQRS;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}
