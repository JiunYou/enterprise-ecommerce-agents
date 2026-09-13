using System;
using System.Collections.Generic;

namespace EnterpriseCommerce.Application.Marketing.Reviews.Queries.GetProductReviews;

/// <summary>
/// 商品評論分頁回應 DTO
/// </summary>
public sealed record ProductReviewsResponse(
    IReadOnlyList<ProductReviewItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>
/// 單筆商品評論公開回應 DTO（絕不洩漏 CustomerId 或內部 Review Id）
/// </summary>
public sealed record ProductReviewItemResponse(
    int Rating,
    string Comment,
    DateTimeOffset CreatedAt);
