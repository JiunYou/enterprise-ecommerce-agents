using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Common.Models;
using EnterpriseCommerce.Domain.Primitives;
using FluentValidation;

namespace EnterpriseCommerce.Application.Catalog.Queries.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 10,
    bool? OnlyActive = true,
    string? SearchTerm = null,
    string? SortBy = null,
    string? SortOrder = null) : IQuery<PagedList<ProductResponse>>;

public sealed class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    private static readonly string[] AllowedSortBy = ["name", "price"];
    private static readonly string[] AllowedSortOrder = ["asc", "desc"];

    public GetProductsQueryValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("頁碼必須大於 0。");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("每頁數量必須大於 0。")
            .LessThanOrEqualTo(100)
            .WithMessage("每頁數量不能超過 100。");

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || AllowedSortBy.Contains(sortBy.Trim().ToLowerInvariant()))
            .WithMessage("排序欄位僅支援 'name' 或 'price'。");

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => string.IsNullOrWhiteSpace(sortOrder) || AllowedSortOrder.Contains(sortOrder.Trim().ToLowerInvariant()))
            .WithMessage("排序方向僅支援 'asc' 或 'desc'。");
    }
}

internal sealed class GetProductsQueryHandler : IQueryHandler<GetProductsQuery, PagedList<ProductResponse>>
{
    private readonly IProductRepository _productRepository;

    public GetProductsQueryHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Result<PagedList<ProductResponse>>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var (products, totalCount) = await _productRepository.GetPagedAsync(
            request.Page,
            request.PageSize,
            request.OnlyActive,
            request.SearchTerm,
            request.SortBy,
            request.SortOrder,
            cancellationToken);

        var productResponses = products
            .Select(p => new ProductResponse(
                p.Id,
                p.Name,
                p.Sku,
                p.Price,
                p.Currency,
                p.IsActive))
            .ToList();

        var pagedList = PagedList<ProductResponse>.Create(
            productResponses,
            request.Page,
            request.PageSize,
            totalCount);

        return Result.Success(pagedList);
    }
}
