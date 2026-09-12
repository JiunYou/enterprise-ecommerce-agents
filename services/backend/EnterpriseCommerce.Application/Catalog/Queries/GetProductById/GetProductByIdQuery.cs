using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Application.Common.CQRS;

namespace EnterpriseCommerce.Application.Catalog.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId, bool AllowInactive = false) : IQuery<ProductDetailResponse>;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    bool IsActive,
    string ImageUrl);

public sealed record ProductDetailResponse(
    Guid Id,
    string Name,
    string Sku,
    decimal Price,
    string Currency,
    bool IsActive,
    string Description,
    string ImageUrl);

internal sealed class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDetailResponse>
{
    private readonly IProductRepository _productRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<ProductDetailResponse>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result.Failure<ProductDetailResponse>(ProductErrors.NotFound);
        }

        if (!product.IsActive && !request.AllowInactive)
        {
            return Result.Failure<ProductDetailResponse>(ProductErrors.NotFound);
        }

        var response = new ProductDetailResponse(
            product.Id,
            product.Name,
            product.Sku,
            product.Price,
            product.Currency,
            product.IsActive,
            product.Description,
            product.ImageUrl);

        return Result.Success(response);
    }
}
