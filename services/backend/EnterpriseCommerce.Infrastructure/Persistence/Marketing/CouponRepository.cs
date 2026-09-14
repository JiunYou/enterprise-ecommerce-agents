using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Domain.Marketing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Infrastructure.Persistence.Marketing;

internal sealed class CouponRepository : ICouponRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public CouponRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Coupon?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Coupons
            .FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);
    }

    public async Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Coupons
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Coupon>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Coupons
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Coupons
            .AnyAsync(c => c.Code == normalizedCode, cancellationToken);
    }

    public void Add(Coupon coupon)
    {
        _dbContext.Coupons.Add(coupon);
    }
}
