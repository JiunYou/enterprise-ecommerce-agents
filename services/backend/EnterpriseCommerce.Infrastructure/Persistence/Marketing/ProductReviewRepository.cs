using EnterpriseCommerce.Application.Marketing.Reviews;
using EnterpriseCommerce.Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Infrastructure.Persistence.Marketing;

internal sealed class ProductReviewRepository : IProductReviewRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public ProductReviewRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> ExistsAsync(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProductReviews
            .AsNoTracking()
            .AnyAsync(
                r => r.CustomerId == customerId && r.ProductId == productId,
                cancellationToken);
    }

    public void Add(ProductReview review)
    {
        _dbContext.ProductReviews.Add(review);
    }

    public async Task<(IReadOnlyList<ProductReview> Items, int TotalCount, double? AverageRating)> GetPagedByProductAsync(
        Guid productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId);

        var totalCount = await query.CountAsync(cancellationToken);

        double? averageRating = totalCount > 0
            ? await query.AverageAsync(r => (double)r.Rating, cancellationToken)
            : null;

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount, averageRating);
    }
}
