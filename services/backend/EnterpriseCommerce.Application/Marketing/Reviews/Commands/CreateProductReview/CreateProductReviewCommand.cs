using EnterpriseCommerce.Application.Common.CQRS;
using System;

namespace EnterpriseCommerce.Application.Marketing.Reviews.Commands.CreateProductReview;

/// <summary>
/// 建立顧客商品評論指令
/// </summary>
public sealed record CreateProductReviewCommand(
    Guid CustomerId,
    Guid ProductId,
    int Rating,
    string Comment) : ICommand;
