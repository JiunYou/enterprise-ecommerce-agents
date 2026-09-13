using EnterpriseCommerce.Domain.Marketing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Reviews;

/// <summary>
/// 顧客商品評論倉儲介面
/// </summary>
public interface IProductReviewRepository
{
    /// <summary>
    /// 檢查特定顧客是否已對指定商品撰寫過評論
    /// </summary>
    Task<bool> ExistsAsync(Guid customerId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增商品評論
    /// </summary>
    void Add(ProductReview review);

    /// <summary>
    /// 依商品識別碼分頁取得評論列表、總筆數與全體評論平均評分
    /// </summary>
    Task<(IReadOnlyList<ProductReview> Items, int TotalCount, double? AverageRating)> GetPagedByProductAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
