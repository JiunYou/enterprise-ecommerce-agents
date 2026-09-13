using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Reviews.Queries.GetProductReviews;

/// <summary>
/// 取得商品評論分頁查詢處理常式
/// </summary>
public sealed class GetProductReviewsQueryHandler : IQueryHandler<GetProductReviewsQuery, ProductReviewsResponse>
{
    private readonly IProductReviewRepository _productReviewRepository;
    private readonly IProductRepository _productRepository;

    public GetProductReviewsQueryHandler(
        IProductReviewRepository productReviewRepository,
        IProductRepository productRepository)
    {
        _productReviewRepository = productReviewRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<ProductReviewsResponse>> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
    {
        // 1. 驗證商品是否存在且已上架（未上架商品回傳 404 NotFound，不對外洩露未上架商品）
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !product.IsActive)
        {
            return Result.Failure<ProductReviewsResponse>(ProductErrors.NotFound);
        }

        // 2. 規範分頁邊界參數：預設 page=1, pageSize=10，最大 pageSize=50
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 50);

        // 3. 執行倉儲分頁查詢
        var (items, totalCount, averageRating) = await _productReviewRepository.GetPagedByProductAsync(
            request.ProductId,
            page,
            pageSize,
            cancellationToken);

        // 4. 映射為公開 DTO（嚴格杜絕暴露 CustomerId 或內部 Review Id）
        var responseItems = items
            .Select(r => new ProductReviewItemResponse(r.Rating, r.Comment, r.CreatedAt))
            .ToList();

        return Result.Success(new ProductReviewsResponse(responseItems, page, pageSize, totalCount, averageRating));
    }
}
