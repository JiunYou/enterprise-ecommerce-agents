using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Marketing;

/// <summary>
/// 優惠券領域錯誤代碼定義
/// </summary>
public static class CouponErrors
{
    public static readonly Error InvalidId = new(
        "Coupon.InvalidId",
        "優惠券識別碼無效。");

    public static readonly Error InvalidCode = new(
        "Coupon.InvalidCode",
        "優惠券代碼格式無效。");

    public static readonly Error InvalidDiscountAmount = new(
        "Coupon.InvalidDiscountAmount",
        "優惠券折扣金額必須大於零。");

    public static readonly Error InvalidCurrency = new(
        "Coupon.InvalidCurrency",
        "優惠券幣別代碼無效。");

    public static readonly Error InvalidTimeWindow = new(
        "Coupon.InvalidTimeWindow",
        "優惠券有效時間區間無效，結束時間必須晚於開始時間。");

    public static readonly Error AlreadyDeactivated = new(
        "Coupon.AlreadyDeactivated",
        "優惠券已停用。");

    public static readonly Error Inactive = new(
        "Coupon.Inactive",
        "優惠券目前未啟用。");

    public static readonly Error NotStarted = new(
        "Coupon.NotStarted",
        "優惠券活動尚未開始。");

    public static readonly Error Expired = new(
        "Coupon.Expired",
        "優惠券已過期。");

    public static readonly Error CurrencyMismatch = new(
        "Coupon.CurrencyMismatch",
        "優惠券幣別與訂單幣別不符。");

    public static readonly Error InvalidOrUnavailable = new(
        "Coupon.InvalidOrUnavailable",
        "優惠券無效或目前無法使用。");

    public static readonly Error NotFound = new(
        "Coupon.NotFound",
        "找不到指定的優惠券。");

    public static readonly Error Conflict = new(
        "Coupon.Conflict",
        "已存在相同代碼之優惠券。");
}
