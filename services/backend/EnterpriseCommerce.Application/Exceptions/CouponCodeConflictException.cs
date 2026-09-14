using System;

namespace EnterpriseCommerce.Application.Exceptions;

/// <summary>
/// 提供者中立之優惠券代碼唯一約束衝突例外。
/// 由基礎設施層於資料庫層級發生 Coupons.Code 唯一鍵違規時拋出，
/// 供應用層 CommandHandler 安全轉譯為領域衝突錯誤，絕不洩漏底層資料庫內部細節。
/// </summary>
public sealed class CouponCodeConflictException : Exception
{
    public CouponCodeConflictException(string message = "Coupon with this code already exists.", Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
