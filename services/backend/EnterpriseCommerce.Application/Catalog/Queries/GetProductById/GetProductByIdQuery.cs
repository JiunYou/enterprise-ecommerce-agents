using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Catalog.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId, bool AllowInactive = false) : IQuery<ProductResponse>;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    bool IsActive);

internal sealed class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductResponse>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<ProductResponse>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductResponse>(ProductErrors.NotFound);
        }

        if (!product.IsActive && !request.AllowInactive)
        {
            return Result.Failure<ProductResponse>(ProductErrors.NotFound);
        }

        var response = new ProductResponse(
            product.Id,
            product.Name,
            product.Sku,
            product.Price,
            product.Currency,
            product.IsActive);

        return Result.Success(response);
    }
}
