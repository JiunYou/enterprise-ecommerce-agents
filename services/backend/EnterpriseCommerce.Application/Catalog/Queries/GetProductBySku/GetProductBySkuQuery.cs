using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Queries.GetProductBySku;

public sealed record GetProductBySkuQuery(string Sku, bool AllowInactive = false) : IQuery<ProductResponse>;

public sealed class GetProductBySkuQueryValidator : AbstractValidator<GetProductBySkuQuery>
{
    public GetProductBySkuQueryValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty()
            .WithMessage("SKU 不能為空。")
            .MaximumLength(100)
            .WithMessage("SKU 長度不能超過 100 字元。");
    }
}

internal sealed class GetProductBySkuQueryHandler : IQueryHandler<GetProductBySkuQuery, ProductResponse>
{
    private readonly IProductRepository _productRepository;

    public GetProductBySkuQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<ProductResponse>> Handle(GetProductBySkuQuery request, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetBySkuAsync(request.Sku, cancellationToken);
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
