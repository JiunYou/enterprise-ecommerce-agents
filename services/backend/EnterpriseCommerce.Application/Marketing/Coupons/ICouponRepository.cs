using EnterpriseCommerce.Domain.Marketing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseCommerce.Application.Marketing.Coupons;

/// <summary>
/// 優惠券倉儲介面 (Marketing Context)
/// </summary>
public interface ICouponRepository
{
    Task<Coupon?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);
    Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Coupon>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);
    void Add(Coupon coupon);
}
