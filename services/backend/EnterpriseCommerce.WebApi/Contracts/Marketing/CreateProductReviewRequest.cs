namespace EnterpriseCommerce.WebApi.Contracts.Marketing;

/// <summary>
/// 建立商品評論請求契約
/// </summary>
public sealed record CreateProductReviewRequest(
    int Rating,
    string Comment);
