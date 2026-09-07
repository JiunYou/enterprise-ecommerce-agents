using System.Globalization;
using System.Net;
using System.Text.Json;
using EnterpriseCommerce.Application.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

/// <summary>
/// 綠界金流 (ECPay AIO Credit) 退款提供者實作。
/// 支援原創全額退款執行（先 QueryTrade 再依狀態矩陣執行 DoAction）與純查詢對帳。
/// 具備 Fail-Closed 防護、台灣時間 20:15–20:30 禁運時段防護與絕對不重試安全保證。
/// </summary>
public sealed class ECPayRefundProvider : IPaymentRefundProvider
{
    public const string ECPayProviderName = "ECPay";

    private static readonly TimeZoneInfo TaiwanTimeZone = ResolveTaiwanTimeZone();
    private static readonly TimeSpan BlackoutStartTime = new(20, 15, 0);
    private static readonly TimeSpan BlackoutEndTime = new(20, 30, 0);

    private readonly HttpClient _httpClient;
    private readonly ECPayRefundOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ECPayRefundProvider>? _logger;

    public string ProviderName => ECPayProviderName;

    public ECPayRefundProvider(
        HttpClient httpClient,
        IOptions<ECPayRefundOptions> options,
        TimeProvider? timeProvider = null,
        ILogger<ECPayRefundProvider>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    public ECPayRefundProvider(
        HttpClient httpClient,
        ECPayRefundOptions options,
        TimeProvider? timeProvider = null,
        ILogger<ECPayRefundProvider>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>
    /// 執行退款意圖建立前之提供者就緒狀態檢查。
    /// 嚴格遵循資金安全原則：READINESS_DOACTION_COUNT=0。
    /// 執行順序：驗證 request -> 驗證基礎組態 -> 檢查台灣時間禁運時段（Blackout 前置）-> QueryTrade 查詢 -> 狀態分類 -> 驗證 DoAction 組態（僅針對 Ready 狀態）。
    /// </summary>
    public async Task<RefundReadinessResult> CheckExecutionReadinessAsync(
        RefundExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 驗證金額與幣別（Money Rules）
        if (!IsSupportedCurrency(request.Currency) || !IsValidIntegralAmount(request.Amount))
        {
            _logger?.LogWarning("Invalid currency ({Currency}) or amount ({Amount}) during readiness check.",
                request.Currency, request.Amount);
            return new RefundReadinessResult(
                RefundReadinessOutcome.ManualProviderResolutionRequired,
                Details: "Currency must be TWD and amount must be a positive integer.");
        }

        // 2. 驗證授權單號（ProviderAuthorizationReference / gwsr）
        if (string.IsNullOrWhiteSpace(request.ProviderAuthorizationReference) ||
            request.ProviderAuthorizationReference.Length > 100 ||
            !IsValidCreditRefundId(request.ProviderAuthorizationReference))
        {
            _logger?.LogWarning("Invalid or missing ProviderAuthorizationReference for payment attempt {PaymentAttemptId} during readiness check.",
                request.PaymentAttemptId);
            return new RefundReadinessResult(
                RefundReadinessOutcome.ManualProviderResolutionRequired,
                Details: "Missing or invalid ProviderAuthorizationReference (ECPay gwsr).");
        }

        // 3. 驗證交易單號（ProviderTransactionId / TradeNo）
        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId))
        {
            _logger?.LogWarning("Missing ProviderTransactionId during readiness check.");
            return new RefundReadinessResult(
                RefundReadinessOutcome.ManualProviderResolutionRequired,
                Details: "Missing ProviderTransactionId required for potential execution.");
        }

        // 4. 驗證基礎組態（發起 HTTP 前 Fail-Closed，不外洩機密）
        _options.Validate(requireDoActionUrl: false);

        // 5. 檢查台灣時間 20:15–20:30 禁運時段（Section 12: 必須在 QueryTrade 之前判定，Query=0, DoAction=0）
        if (IsInTaiwanBlackoutWindow(_timeProvider.GetUtcNow()))
        {
            _logger?.LogWarning("ECPay refund readiness blocked due to daily 20:15–20:30 blackout window.");
            return new RefundReadinessResult(
                RefundReadinessOutcome.ExecutionTemporarilyBlocked,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "ECPay refund readiness is temporarily blocked during daily 20:15–20:30 Taiwan time.");
        }

        // 6. 發起 QueryTrade/V2 查詢當前提供者狀態
        var queryResult = await ExecuteQueryTradeAsync(request, cancellationToken);
        if (!queryResult.IsSuccess)
        {
            return new RefundReadinessResult(
                RefundReadinessOutcome.Unresolved,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: queryResult.ErrorMessage);
        }

        // 7. 依查詢狀態分類進行就緒判定
        switch (queryResult.State)
        {
            case ECPayTradeClassifiedState.AlreadyRefunded:
                return new RefundReadinessResult(
                    RefundReadinessOutcome.AlreadyRefunded,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Provider confirmed transaction is already refunded or cancelled.");

            case ECPayTradeClassifiedState.ManualResolutionRequired:
                return new RefundReadinessResult(
                    RefundReadinessOutcome.ManualProviderResolutionRequired,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Provider transaction status cannot be safely refunded automatically.");

            case ECPayTradeClassifiedState.Unresolved:
                return new RefundReadinessResult(
                    RefundReadinessOutcome.Unresolved,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Provider transaction state is unknown or malformed.");

            case ECPayTradeClassifiedState.Authorized:
            case ECPayTradeClassifiedState.ToBeCaptured:
            case ECPayTradeClassifiedState.Captured:
                // 若狀態為可退款/可操作，確保 DoActionUrl 亦合法方可回傳 Ready
                try
                {
                    _options.Validate(requireDoActionUrl: true);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "ECPay DoActionUrl validation failed during readiness check.");
                    return new RefundReadinessResult(
                        RefundReadinessOutcome.ManualProviderResolutionRequired,
                        ProviderTransactionId: request.ProviderTransactionId,
                        Details: "DoActionUrl is invalid or missing, cannot proceed to Ready.");
                }

                return new RefundReadinessResult(
                    RefundReadinessOutcome.Ready,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: $"Provider state {queryResult.State} is ready for durable intent creation.");

            default:
                return new RefundReadinessResult(
                    RefundReadinessOutcome.Unresolved,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Unrecognized classified state during readiness check.");
        }
    }

    /// <summary>
    /// 執行原創全額退款操作。
    /// 流程：驗證輸入 -> 驗證設定 -> 查詢 QueryTrade/V2 -> 判斷禁運時段 -> 依狀態矩陣執行 DoAction。
    /// </summary>
    public async Task<RefundExecutionResult> ExecuteRefundAsync(
        RefundExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 金額與貨幣防護（Money Rules）
        if (!IsSupportedCurrency(request.Currency) || !IsValidIntegralAmount(request.Amount))
        {
            _logger?.LogWarning("Invalid currency ({Currency}) or non-positive/non-integral amount ({Amount}) for ECPay refund.",
                request.Currency, request.Amount);
            return new RefundExecutionResult(
                RefundExecutionOutcome.ManualProviderResolutionRequired,
                Details: "Currency must be TWD and amount must be a positive integer.");
        }

        // 2. 驗證授權單號（ProviderAuthorizationReference / gwsr）
        if (string.IsNullOrWhiteSpace(request.ProviderAuthorizationReference) ||
            request.ProviderAuthorizationReference.Length > 100 ||
            !IsValidCreditRefundId(request.ProviderAuthorizationReference))
        {
            _logger?.LogWarning("Invalid or missing ProviderAuthorizationReference for payment attempt {PaymentAttemptId}.",
                request.PaymentAttemptId);
            return new RefundExecutionResult(
                RefundExecutionOutcome.ManualProviderResolutionRequired,
                Details: "Missing or invalid ProviderAuthorizationReference (ECPay gwsr).");
        }

        // 3. 驗證基礎組態（發起 HTTP 前 Fail-Closed）
        try
        {
            _options.Validate(requireDoActionUrl: false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ECPay refund options validation failed before querying trade.");
            throw;
        }

        // 4. 發起 QueryTrade/V2 查詢交易當前狀態（QueryTrade 必定先於任何資金異動）
        var queryResult = await ExecuteQueryTradeAsync(request, cancellationToken);
        if (!queryResult.IsSuccess)
        {
            return new RefundExecutionResult(queryResult.FailureOutcome, Details: queryResult.ErrorMessage);
        }

        var classifiedState = queryResult.State;

        // 5. 根據查詢狀態分類進行決策
        switch (classifiedState)
        {
            case ECPayTradeClassifiedState.AlreadyRefunded:
                return new RefundExecutionResult(
                    RefundExecutionOutcome.AlreadyRefunded,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Provider confirmed transaction is already refunded or cancelled.");

            case ECPayTradeClassifiedState.ManualResolutionRequired:
                return new RefundExecutionResult(
                    RefundExecutionOutcome.ManualProviderResolutionRequired,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Provider transaction status cannot be safely refunded automatically.");

            case ECPayTradeClassifiedState.Unresolved:
                return new RefundExecutionResult(
                    RefundExecutionOutcome.Unresolved,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Provider transaction state is unknown or malformed.");

            case ECPayTradeClassifiedState.Authorized:
            case ECPayTradeClassifiedState.ToBeCaptured:
            case ECPayTradeClassifiedState.Captured:
                // 需執行 DoAction 資金異動
                break;

            default:
                return new RefundExecutionResult(
                    RefundExecutionOutcome.Unresolved,
                    ProviderTransactionId: request.ProviderTransactionId,
                    Details: "Unrecognized classified state.");
        }

        // 6. 檢查台灣時間 20:15–20:30 禁運時段（Blackout Window Guard）
        if (IsInTaiwanBlackoutWindow(_timeProvider.GetUtcNow()))
        {
            _logger?.LogWarning("ECPay DoAction blocked due to daily 20:15–20:30 blackout window.");
            return new RefundExecutionResult(
                RefundExecutionOutcome.ExecutionTemporarilyBlocked,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "ECPay DoAction is temporarily blocked during daily 20:15–20:30 Taiwan time.");
        }

        // 7. 驗證 DoAction 專用組態與交易單號
        try
        {
            _options.Validate(requireDoActionUrl: true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ECPay DoAction options validation failed.");
            throw;
        }

        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId))
        {
            _logger?.LogWarning("Missing ProviderTransactionId (TradeNo) for DoAction.");
            return new RefundExecutionResult(
                RefundExecutionOutcome.ManualProviderResolutionRequired,
                Details: "Missing ProviderTransactionId required for DoAction.");
        }

        // 8. 依據狀態矩陣執行 DoAction
        return classifiedState switch
        {
            ECPayTradeClassifiedState.Authorized =>
                await ExecuteSingleActionAsync(request, "N", cancellationToken),

            ECPayTradeClassifiedState.Captured =>
                await ExecuteSingleActionAsync(request, "R", cancellationToken),

            ECPayTradeClassifiedState.ToBeCaptured =>
                await ExecuteCaptureCancellationThenAbandonAsync(request, cancellationToken),

            _ => new RefundExecutionResult(
                RefundExecutionOutcome.Unresolved,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "Unexpected action matrix state.")
        };
    }

    /// <summary>
    /// 純查詢對帳方法。嚴禁發起任何 DoAction 資金異動。
    /// </summary>
    public async Task<RefundReconciliationResult> ReconcileRefundAsync(
        RefundReconciliationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsSupportedCurrency(request.Currency) || !IsValidIntegralAmount(request.Amount))
        {
            return new RefundReconciliationResult(
                RefundReconciliationOutcome.ManualProviderResolutionRequired,
                Details: "Invalid currency or amount.");
        }

        if (string.IsNullOrWhiteSpace(request.ProviderAuthorizationReference) ||
            !IsValidCreditRefundId(request.ProviderAuthorizationReference))
        {
            return new RefundReconciliationResult(
                RefundReconciliationOutcome.ManualProviderResolutionRequired,
                Details: "Missing or invalid ProviderAuthorizationReference.");
        }

        _options.Validate(requireDoActionUrl: false);

        var queryExecutionRequest = new RefundExecutionRequest(
            request.PaymentAttemptId,
            request.ProviderTransactionId,
            request.ProviderAuthorizationReference,
            request.Amount,
            request.Currency);

        var queryResult = await ExecuteQueryTradeAsync(queryExecutionRequest, cancellationToken);
        if (!queryResult.IsSuccess)
        {
            return new RefundReconciliationResult(
                RefundReconciliationOutcome.Unresolved,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: queryResult.ErrorMessage);
        }

        return queryResult.State switch
        {
            ECPayTradeClassifiedState.AlreadyRefunded => new RefundReconciliationResult(
                RefundReconciliationOutcome.AlreadyRefunded,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "Provider confirmed transaction is already refunded or cancelled."),

            ECPayTradeClassifiedState.Authorized or
            ECPayTradeClassifiedState.ToBeCaptured or
            ECPayTradeClassifiedState.Captured or
            ECPayTradeClassifiedState.ManualResolutionRequired => new RefundReconciliationResult(
                RefundReconciliationOutcome.ManualProviderResolutionRequired,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "Provider proves transaction is not refunded."),

            _ => new RefundReconciliationResult(
                RefundReconciliationOutcome.Unresolved,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "Unresolved provider reconciliation state.")
        };
    }

    /// <summary>
    /// 執行 QueryTrade/V2 查詢並分類狀態。
    /// </summary>
    private async Task<QueryTradeInternalResult> ExecuteQueryTradeAsync(
        RefundExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MerchantID"] = _options.MerchantId!,
            ["CreditRefundId"] = request.ProviderAuthorizationReference!,
            ["CreditAmount"] = ((long)request.Amount).ToString(CultureInfo.InvariantCulture),
            ["CreditCheckCode"] = _options.CreditCheckCode!
        };

        var checkMac = ECPayCheckMacValue.Generate(form, _options.HashKey!, _options.HashIv!);
        form["CheckMacValue"] = checkMac;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _options.QueryTradeUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ECPay QueryTrade HTTP transport failed.");
            return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "HTTP transport failure during QueryTrade.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger?.LogWarning("ECPay QueryTrade returned HTTP 403 Forbidden (possible rate limiting).");
                return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "ECPay QueryTrade returned HTTP 403 Forbidden.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("ECPay QueryTrade returned non-success HTTP status {StatusCode}.", response.StatusCode);
                return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, $"ECPay QueryTrade returned HTTP {(int)response.StatusCode}.");
            }

            string responseBody;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to read QueryTrade response content.");
                return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "Failed to read QueryTrade response content.");
            }

            return ParseQueryTradeResponse(responseBody);
        }
    }

    /// <summary>
    /// 解析 QueryTrade/V2 回應之 JSON 格式並分類。
    /// </summary>
    public static QueryTradeInternalResult ParseQueryTradeResponse(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "QueryTrade response is empty.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonContent);
        }
        catch (Exception)
        {
            return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "QueryTrade response is malformed JSON.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "QueryTrade root element is not an object.");
            }

            // 檢查 RtnMsg
            if (root.TryGetProperty("RtnMsg", out var rtnMsgProp))
            {
                var rtnMsg = rtnMsgProp.GetString();
                if (!string.IsNullOrEmpty(rtnMsg))
                {
                    return QueryTradeInternalResult.Failure(
                        RefundExecutionOutcome.Unresolved,
                        $"QueryTrade returned non-empty RtnMsg: {rtnMsg}");
                }
            }

            // 檢查 RtnValue
            if (!root.TryGetProperty("RtnValue", out var rtnValueProp) || rtnValueProp.ValueKind != JsonValueKind.Object)
            {
                return QueryTradeInternalResult.Failure(RefundExecutionOutcome.Unresolved, "QueryTrade response missing valid RtnValue object.");
            }

            // 讀取 status
            string? topStatus = null;
            if (rtnValueProp.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
            {
                topStatus = statusProp.GetString()?.Trim();
            }

            // 檢查 close_data
            var closeDataStatuses = new List<string>();
            if (rtnValueProp.TryGetProperty("close_data", out var closeDataProp))
            {
                if (closeDataProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in closeDataProp.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object &&
                            item.TryGetProperty("status", out var itemStatus) &&
                            itemStatus.ValueKind == JsonValueKind.String)
                        {
                            var s = itemStatus.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(s))
                            {
                                closeDataStatuses.Add(s);
                            }
                        }
                    }
                }
                else if (closeDataProp.ValueKind == JsonValueKind.Object)
                {
                    if (closeDataProp.TryGetProperty("status", out var itemStatus) &&
                        itemStatus.ValueKind == JsonValueKind.String)
                    {
                        var s = itemStatus.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(s))
                        {
                            closeDataStatuses.Add(s);
                        }
                    }
                }
            }

            var hasToBeCaptured = closeDataStatuses.Any(s => string.Equals(s, "要關帳", StringComparison.Ordinal));
            var hasCaptured = closeDataStatuses.Any(s => string.Equals(s, "已關帳", StringComparison.Ordinal));
            var hasCancelled = closeDataStatuses.Any(s => string.Equals(s, "已取消", StringComparison.Ordinal));
            var hasActionCancelled = closeDataStatuses.Any(s => string.Equals(s, "操作取消", StringComparison.Ordinal));

            // 1. 若 close_data 中含有 active「要關帳」，優先判定為 ToBeCaptured（需 E -> N）
            if (hasToBeCaptured)
            {
                return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.ToBeCaptured);
            }

            // 2. 若頂層狀態為「已授權」
            if (string.Equals(topStatus, "已授權", StringComparison.Ordinal))
            {
                if (hasCaptured)
                {
                    return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.Captured);
                }
                if (hasCancelled)
                {
                    return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.AlreadyRefunded);
                }
                return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.Authorized);
            }

            // 3. 若頂層狀態為「已關帳」或 close_data 顯示已關帳
            if (string.Equals(topStatus, "已關帳", StringComparison.Ordinal) || hasCaptured)
            {
                return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.Captured);
            }

            // 4. 若頂層狀態為「已取消」或明確證明全額取消
            if (string.Equals(topStatus, "已取消", StringComparison.Ordinal))
            {
                return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.AlreadyRefunded);
            }

            // 5. 操作取消：極度保守處理，若無法無歧義證明已全額退款，一律回傳 ManualResolutionRequired
            if (string.Equals(topStatus, "操作取消", StringComparison.Ordinal) || hasActionCancelled)
            {
                return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.ManualResolutionRequired);
            }

            // 6. 未授權
            if (string.Equals(topStatus, "未授權", StringComparison.Ordinal))
            {
                return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.ManualResolutionRequired);
            }

            // 7. 其他未知狀態
            return QueryTradeInternalResult.Success(ECPayTradeClassifiedState.Unresolved);
        }
    }

    /// <summary>
    /// 執行單一 DoAction（如 "N" 取消授權、或 "R" 退刷）。
    /// </summary>
    private async Task<RefundExecutionResult> ExecuteSingleActionAsync(
        RefundExecutionRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        var actionResult = await SendDoActionHttpAsync(request, action, cancellationToken);
        if (actionResult.Outcome == DoActionWireOutcome.Success)
        {
            return new RefundExecutionResult(
                RefundExecutionOutcome.RefundCompleted,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: $"DoAction {action} succeeded.");
        }

        if (actionResult.Outcome == DoActionWireOutcome.DeterministicFailure)
        {
            return new RefundExecutionResult(
                RefundExecutionOutcome.ManualProviderResolutionRequired,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: $"DoAction {action} failed deterministically with code {actionResult.RtnCode}: {actionResult.RtnMsg}");
        }

        // 不確定或通訊異常，嚴禁自動重試
        return new RefundExecutionResult(
            RefundExecutionOutcome.Unresolved,
            ProviderTransactionId: request.ProviderTransactionId,
            Details: $"DoAction {action} outcome is uncertain: {actionResult.RtnMsg}");
    }

    /// <summary>
    /// 執行 ToBeCaptured 的關帳取消再取消授權序列（E then N）。
    /// 必須在同一次執行中完成，若 E 失敗或不確定，絕不呼叫 N。
    /// </summary>
    private async Task<RefundExecutionResult> ExecuteCaptureCancellationThenAbandonAsync(
        RefundExecutionRequest request,
        CancellationToken cancellationToken)
    {
        // 1. 先執行 DoAction "E"（關帳取消）
        var eResult = await SendDoActionHttpAsync(request, "E", cancellationToken);
        if (eResult.Outcome == DoActionWireOutcome.DeterministicFailure)
        {
            _logger?.LogWarning("DoAction E failed deterministically with code {RtnCode}: {RtnMsg}. Halting without calling N.",
                eResult.RtnCode, eResult.RtnMsg);
            return new RefundExecutionResult(
                RefundExecutionOutcome.ManualProviderResolutionRequired,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: $"DoAction E failed with code {eResult.RtnCode}: {eResult.RtnMsg}");
        }

        if (eResult.Outcome != DoActionWireOutcome.Success)
        {
            _logger?.LogWarning("DoAction E outcome uncertain ({Outcome}): {RtnMsg}. Halting without calling N.",
                eResult.Outcome, eResult.RtnMsg);
            return new RefundExecutionResult(
                RefundExecutionOutcome.Unresolved,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: $"DoAction E outcome is uncertain: {eResult.RtnMsg}");
        }

        // 2. 僅當 E 確認成功時，才接續執行 DoAction "N"（取消授權）
        var nResult = await SendDoActionHttpAsync(request, "N", cancellationToken);
        if (nResult.Outcome == DoActionWireOutcome.Success)
        {
            return new RefundExecutionResult(
                RefundExecutionOutcome.RefundCompleted,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: "DoAction E then N succeeded.");
        }

        if (nResult.Outcome == DoActionWireOutcome.DeterministicFailure)
        {
            return new RefundExecutionResult(
                RefundExecutionOutcome.ManualProviderResolutionRequired,
                ProviderTransactionId: request.ProviderTransactionId,
                Details: $"DoAction N failed after E with code {nResult.RtnCode}: {nResult.RtnMsg}");
        }

        return new RefundExecutionResult(
            RefundExecutionOutcome.Unresolved,
            ProviderTransactionId: request.ProviderTransactionId,
            Details: $"DoAction N outcome uncertain after E: {nResult.RtnMsg}");
    }

    /// <summary>
    /// 發送 DoAction 實體 HTTP POST 請求並解析經典 parameter-style 回應。
    /// 嚴禁自動重試。
    /// </summary>
    private async Task<DoActionInternalResult> SendDoActionHttpAsync(
        RefundExecutionRequest request,
        string action,
        CancellationToken cancellationToken)
    {
        var merchantTradeNo = ECPayMerchantTradeNo.FromPaymentAttemptId(request.PaymentAttemptId);

        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MerchantID"] = _options.MerchantId!,
            ["MerchantTradeNo"] = merchantTradeNo,
            ["TradeNo"] = request.ProviderTransactionId!,
            ["Action"] = action,
            ["TotalAmount"] = ((long)request.Amount).ToString(CultureInfo.InvariantCulture)
        };

        var checkMac = ECPayCheckMacValue.Generate(form, _options.HashKey!, _options.HashIv!);
        form["CheckMacValue"] = checkMac;

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, _options.DoActionUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ECPay DoAction {Action} transport failed.", action);
            return DoActionInternalResult.Uncertain("Transport failure during DoAction.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("ECPay DoAction {Action} returned HTTP {StatusCode}.", action, response.StatusCode);
                return DoActionInternalResult.Uncertain($"DoAction returned HTTP {(int)response.StatusCode}.");
            }

            string responseBody;
            try
            {
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to read DoAction response content.");
                return DoActionInternalResult.Uncertain("Failed to read DoAction response content.");
            }

            return ParseDoActionResponse(responseBody);
        }
    }

    /// <summary>
    /// 解析 DoAction 經典 parameter-style 回應文字（非 JSON）。
    /// </summary>
    public static DoActionInternalResult ParseDoActionResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return DoActionInternalResult.Uncertain("Empty DoAction response.");
        }

        var parameters = ParseParameterString(content);
        if (!parameters.TryGetValue("RtnCode", out var rtnCode) || string.IsNullOrWhiteSpace(rtnCode))
        {
            return DoActionInternalResult.Uncertain("Missing RtnCode in DoAction response.");
        }

        parameters.TryGetValue("RtnMsg", out var rtnMsg);

        if (string.Equals(rtnCode.Trim(), "1", StringComparison.Ordinal))
        {
            return DoActionInternalResult.Success(rtnCode.Trim(), rtnMsg);
        }

        return DoActionInternalResult.DeterministicFailure(rtnCode.Trim(), rtnMsg);
    }

    private static Dictionary<string, string> ParseParameterString(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 支援 & 分隔或換行分隔
        var separators = new[] { '&', '\r', '\n' };
        var pairs = raw.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx > 0)
            {
                var key = pair[..idx].Trim();
                var val = pair[(idx + 1)..].Trim();
                dict[key] = val;
            }
        }
        return dict;
    }

    /// <summary>
    /// 判斷特定 UTC 時間轉換為台灣時間後，是否落於 20:15:00 至 20:29:59.999 禁運時段內。
    /// 20:14:59 -> allowed (false)
    /// 20:15:00 -> blocked (true)
    /// 20:29:59 -> blocked (true)
    /// 20:30:00 -> allowed (false)
    /// </summary>
    public static bool IsInTaiwanBlackoutWindow(DateTimeOffset utcNow)
    {
        var taiwanTime = TimeZoneInfo.ConvertTime(utcNow, TaiwanTimeZone);
        var timeOfDay = taiwanTime.TimeOfDay;
        return timeOfDay >= BlackoutStartTime && timeOfDay < BlackoutEndTime;
    }

    private static bool IsSupportedCurrency(string currency) =>
        string.Equals(currency, "TWD", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidIntegralAmount(decimal amount) =>
        amount > 0 && amount % 1 == 0;

    private static bool IsValidCreditRefundId(string gwsr) =>
        !string.IsNullOrWhiteSpace(gwsr) && gwsr.All(char.IsLetterOrDigit);

    private static TimeZoneInfo ResolveTaiwanTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback to custom fixed +08:00 if system time zone data is missing
                return TimeZoneInfo.CreateCustomTimeZone("Taiwan Standard Time", TimeSpan.FromHours(8), "Taiwan Standard Time", "Taiwan Standard Time");
            }
        }
    }
}

public enum ECPayTradeClassifiedState
{
    Authorized,
    ToBeCaptured,
    Captured,
    AlreadyRefunded,
    ManualResolutionRequired,
    Unresolved
}

public sealed class QueryTradeInternalResult
{
    public bool IsSuccess { get; private init; }
    public ECPayTradeClassifiedState State { get; private init; }
    public RefundExecutionOutcome FailureOutcome { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static QueryTradeInternalResult Success(ECPayTradeClassifiedState state) =>
        new() { IsSuccess = true, State = state };

    public static QueryTradeInternalResult Failure(RefundExecutionOutcome outcome, string? message) =>
        new() { IsSuccess = false, FailureOutcome = outcome, ErrorMessage = message };
}

public enum DoActionWireOutcome
{
    Success,
    DeterministicFailure,
    Uncertain
}

public sealed class DoActionInternalResult
{
    public DoActionWireOutcome Outcome { get; private init; }
    public string? RtnCode { get; private init; }
    public string? RtnMsg { get; private init; }

    public static DoActionInternalResult Success(string rtnCode, string? rtnMsg) =>
        new() { Outcome = DoActionWireOutcome.Success, RtnCode = rtnCode, RtnMsg = rtnMsg };

    public static DoActionInternalResult DeterministicFailure(string rtnCode, string? rtnMsg) =>
        new() { Outcome = DoActionWireOutcome.DeterministicFailure, RtnCode = rtnCode, RtnMsg = rtnMsg };

    public static DoActionInternalResult Uncertain(string? rtnMsg) =>
        new() { Outcome = DoActionWireOutcome.Uncertain, RtnMsg = rtnMsg };
}
