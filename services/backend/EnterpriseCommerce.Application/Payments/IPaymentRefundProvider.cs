using EnterpriseCommerce.Domain.Payments.ValueObjects;

namespace EnterpriseCommerce.Application.Payments;

/// <summary>
/// 執行退款操作的語意結果狀態。
/// 嚴格遵循中立語意，不洩漏任何特定第三方金流商之專有名詞或字串。
/// </summary>
public enum RefundExecutionOutcome
{
    /// <summary>
    /// 提供者已成功確認完成全額退款或取消授權。
    /// </summary>
    RefundCompleted,

    /// <summary>
    /// 提供者狀態明確證明先前已完成退款或取消，本次無需重複執行資金異動。
    /// </summary>
    AlreadyRefunded,

    /// <summary>
    /// 提供者狀態或請求資料無法安全自動退款，必須由人工介入審查與處理。
    /// </summary>
    ManualProviderResolutionRequired,

    /// <summary>
    /// 提供者結果不確定（如通訊逾時、網路中斷、解析錯誤或未知狀態），嚴禁自動重試。
    /// </summary>
    Unresolved,

    /// <summary>
    /// 提供者當前處於維護或禁運時段，暫時無法安全執行退款資金異動。
    /// </summary>
    ExecutionTemporarilyBlocked
}

/// <summary>
/// 執行退款操作的請求模型。
/// </summary>
public sealed record RefundExecutionRequest(
    PaymentAttemptId PaymentAttemptId,
    string? ProviderTransactionId,
    string? ProviderAuthorizationReference,
    decimal Amount,
    string Currency);

/// <summary>
/// 執行退款操作的結果模型。
/// </summary>
public sealed record RefundExecutionResult(
    RefundExecutionOutcome Outcome,
    string? ProviderTransactionId = null,
    string? Details = null);

/// <summary>
/// 退款對帳查詢操作的語意結果狀態。
/// 僅用於查詢提供者端退款真實狀態，嚴禁執行任何資金異動。
/// </summary>
public enum RefundReconciliationOutcome
{
    /// <summary>
    /// 提供者狀態明確證明款項已完成退款或取消。
    /// </summary>
    AlreadyRefunded,

    /// <summary>
    /// 提供者狀態明確證明款項尚未退款，或無法透過對帳完成自動結案。
    /// </summary>
    ManualProviderResolutionRequired,

    /// <summary>
    /// 提供者查詢失敗或狀態不明。
    /// </summary>
    Unresolved
}

/// <summary>
/// 退款對帳查詢請求模型。
/// </summary>
public sealed record RefundReconciliationRequest(
    PaymentAttemptId PaymentAttemptId,
    string? ProviderTransactionId,
    string? ProviderAuthorizationReference,
    decimal Amount,
    string Currency);

/// <summary>
/// 退款對帳查詢結果模型。
/// </summary>
public sealed record RefundReconciliationResult(
    RefundReconciliationOutcome Outcome,
    string? ProviderTransactionId = null,
    string? Details = null);

/// <summary>
/// 退款意圖建立前之就緒狀態檢查語意結果。
/// 嚴禁觸發任何資金異動。
/// </summary>
public enum RefundReadinessOutcome
{
    /// <summary>
    /// 提供者前置條件充分（狀態可退且組態有效），允許進入持久化意圖建立階段。
    /// 不代表已執行資金異動，亦不保證後續執行必定成功。
    /// </summary>
    Ready,

    /// <summary>
    /// 提供者狀態明確證明款項先前已完成退款或取消，無需建立退款意圖或重複退款。
    /// </summary>
    AlreadyRefunded,

    /// <summary>
    /// 提供者狀態或資料無法安全自動退款，必須由人工介入審查與處理。
    /// </summary>
    ManualProviderResolutionRequired,

    /// <summary>
    /// 提供者查詢失敗或狀態不明，保留模糊性，嚴禁自動推進。
    /// </summary>
    Unresolved,

    /// <summary>
    /// 提供者當前處於維護或禁運時段，暫時無法安全進行就緒判定或退款。
    /// </summary>
    ExecutionTemporarilyBlocked
}

/// <summary>
/// 退款意圖建立前之就緒狀態檢查結果模型。
/// </summary>
public sealed record RefundReadinessResult(
    RefundReadinessOutcome Outcome,
    string? ProviderTransactionId = null,
    string? Details = null);

/// <summary>
/// 提供者中立之退款提供者介面。
/// 支援原創執行（先查後退）、意圖前就緒檢查（純檢查絕不動錢）與純查詢對帳。
/// </summary>
public interface IPaymentRefundProvider
{
    string ProviderName { get; }

    Task<RefundReadinessResult> CheckExecutionReadinessAsync(
        RefundExecutionRequest request,
        CancellationToken cancellationToken = default);

    Task<RefundExecutionResult> ExecuteRefundAsync(
        RefundExecutionRequest request,
        CancellationToken cancellationToken = default);

    Task<RefundReconciliationResult> ReconcileRefundAsync(
        RefundReconciliationRequest request,
        CancellationToken cancellationToken = default);
}

