using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Catalog.Queries.GetProductCategories;

public sealed record GetProductCategoriesQuery : IQuery<IReadOnlyList<string>>;

internal sealed class GetProductCategoriesQueryHandler : IQueryHandler<GetProductCategoriesQuery, IReadOnlyList<string>>
{
    private readonly IProductRepository _productRepository;

    public GetProductCategoriesQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<IReadOnlyList<string>>> Handle(GetProductCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _productRepository.GetActiveCategoriesAsync(cancellationToken);

        return Result.Success(categories);
    }
}
