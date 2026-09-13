using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Reviews.Commands.CreateProductReview;

/// <summary>
/// 建立顧客商品評論指令處理常式
/// </summary>
public sealed class CreateProductReviewCommandHandler : ICommandHandler<CreateProductReviewCommand>
{
    private readonly IProductReviewRepository _productReviewRepository;
    private readonly IProductRepository _productRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateProductReviewCommandHandler(
        IProductReviewRepository productReviewRepository,
        IProductRepository productRepository,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider? timeProvider = null)
    {
        _productReviewRepository = productReviewRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result> Handle(CreateProductReviewCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == Guid.Empty)
        {
            return Result.Failure(ReviewErrors.InvalidCustomerId);
        }

        if (request.ProductId == Guid.Empty)
        {
            return Result.Failure(ReviewErrors.InvalidProductId);
        }

        // 1. 驗證商品存在且已上架（未上架商品不對外公開，統一回傳 NotFound）
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null || !product.IsActive)
        {
            return Result.Failure(ProductErrors.NotFound);
        }

        // 2. 檢查同一位顧客是否已在此商品發表過評論
        var exists = await _productReviewRepository.ExistsAsync(
            request.CustomerId,
            request.ProductId,
            cancellationToken);

        if (exists)
        {
            return Result.Failure(ReviewErrors.AlreadyExists);
        }

        // 3. 建立評論實體並進行格式與評分驗證
        var createdAt = _timeProvider.GetUtcNow();
        var reviewResult = ProductReview.Create(
            Guid.NewGuid(),
            request.CustomerId,
            request.ProductId,
            request.Rating,
            request.Comment,
            createdAt);

        if (reviewResult.IsFailure)
        {
            return Result.Failure(reviewResult.Error);
        }

        // 4. 加入倉儲並持久化保存
        _productReviewRepository.Add(reviewResult.Value);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
