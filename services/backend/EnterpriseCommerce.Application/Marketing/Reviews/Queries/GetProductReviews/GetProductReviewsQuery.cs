using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Reviews.Queries.GetProductReviews;

/// <summary>
/// 取得商品評論分頁查詢
/// </summary>
public sealed record GetProductReviewsQuery(
    Guid ProductId,
    int Page = 1,
    int PageSize = 10) : IQuery<ProductReviewsResponse>;
