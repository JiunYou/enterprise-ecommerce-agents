using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.Configuration;

namespace EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;

internal sealed class AdminRefundPaymentCommandHandler : ICommandHandler<AdminRefundPaymentCommand, AdminRefundPaymentResult>
{
    private readonly IPaymentAttemptRepository _paymentAttemptRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentRefundRepository _paymentRefundRepository;
    private readonly IPaymentRefundProvider _refundProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public AdminRefundPaymentCommandHandler(
        IPaymentAttemptRepository paymentAttemptRepository,
        IOrderRepository orderRepository,
        IPaymentRefundRepository paymentRefundRepository,
        IPaymentRefundProvider refundProvider,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _paymentAttemptRepository = paymentAttemptRepository;
        _orderRepository = orderRepository;
        _paymentRefundRepository = paymentRefundRepository;
        _refundProvider = refundProvider;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminRefundPaymentResult>> Handle(AdminRefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var paymentAttemptId = new PaymentAttemptId(request.PaymentAttemptId);

        // 1. 載入目標 PaymentAttempt
        var targetAttempt = await _paymentAttemptRepository.GetByIdAsync(paymentAttemptId, cancellationToken);
        if (targetAttempt is null)
        {
            return Result.Failure<AdminRefundPaymentResult>(PaymentErrors.NotFound);
        }

        // 2. 嚴格目標檢查：僅允許 RefundRequired 狀態
        if (targetAttempt.Status != PaymentAttemptStatus.RefundRequired)
        {
            return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.RefundRequiredStatusExpected);
        }

        // 3. 載入關聯 Order
        var order = await _orderRepository.GetByIdAsync(targetAttempt.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Failure<AdminRefundPaymentResult>(OrderErrors.NotFound);
        }

        // 4. 載入該訂單所有 PaymentAttempts
        var allAttempts = await _paymentAttemptRepository.GetByOrderIdAsync(targetAttempt.OrderId, cancellationToken);

        // 5. 檢查是否已存在退款意圖 / 記錄（Existing Refund First）
        var existingRefund = await _paymentRefundRepository.GetByPaymentAttemptIdAsync(targetAttempt.Id, cancellationToken);

        if (existingRefund is not null)
        {
            // 嚴格原則：已存在退款記錄時，絕對不允許調用 ExecuteRefundAsync
            if (existingRefund.Status == PaymentRefundStatus.Succeeded)
            {
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.AlreadyRefunded,
                    PaymentRefundStatus.Succeeded));
            }

            if (existingRefund.Status == PaymentRefundStatus.Failed)
            {
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.ManualProviderResolutionRequired,
                    PaymentRefundStatus.Failed));
            }

            if (existingRefund.Status == PaymentRefundStatus.Pending || existingRefund.Status == PaymentRefundStatus.Unresolved)
            {
                var reconcileRequest = new RefundReconciliationRequest(
                    targetAttempt.Id,
                    targetAttempt.ProviderTransactionId,
                    targetAttempt.ProviderAuthorizationReference,
                    targetAttempt.Amount.Amount,
                    targetAttempt.Amount.Currency);

                var reconResult = await _refundProvider.ReconcileRefundAsync(reconcileRequest, cancellationToken);

                switch (reconResult.Outcome)
                {
                    case RefundReconciliationOutcome.AlreadyRefunded:
                        existingRefund.MarkAsSucceeded(_timeProvider.GetUtcNow());
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.AlreadyRefunded,
                            PaymentRefundStatus.Succeeded));

                    case RefundReconciliationOutcome.Unresolved:
                        if (existingRefund.Status == PaymentRefundStatus.Pending)
                        {
                            existingRefund.MarkAsUnresolved();
                            await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        }
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.Unresolved,
                            PaymentRefundStatus.Unresolved));

                    case RefundReconciliationOutcome.ManualProviderResolutionRequired:
                        if (existingRefund.Status == PaymentRefundStatus.Pending)
                        {
                            existingRefund.MarkAsUnresolved();
                            await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        }
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.ManualProviderResolutionRequired,
                            existingRefund.Status));

                    default:
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.Unresolved,
                            existingRefund.Status));
                }
            }
        }

        // 6. 新退款流程（New Refund）
        // A. 本地退款資格評估
        var expirationStr = _configuration["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"];
        var expirationMinutes = int.TryParse(expirationStr, out var m) ? m : 15;
        var eligibility = RefundEligibilityEvaluator.Evaluate(
            targetAttempt,
            order,
            allAttempts,
            _timeProvider.GetUtcNow(),
            expirationMinutes);

        switch (eligibility)
        {
            case RefundEligibilityStatus.InvariantViolation:
                return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.RefundOrderPaymentInvariantViolation);

            case RefundEligibilityStatus.NotEligible:
                return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.RefundNotEligible);

            case RefundEligibilityStatus.ManualProviderResolutionRequired:
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.ManualProviderResolutionRequired,
                    null));

            case RefundEligibilityStatus.NotRefundRequired:
                return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.RefundRequiredStatusExpected);

            case RefundEligibilityStatus.Eligible:
                break;
        }

        // B. 記憶體中先驗證 PaymentRefund 領域稽核輸入（在調用提供者之前）
        var refundCreateResult = PaymentRefund.Create(
            targetAttempt.Id,
            request.Reason ?? string.Empty,
            request.ActorIssuer,
            request.ActorSubject,
            _timeProvider.GetUtcNow());

        if (refundCreateResult.IsFailure)
        {
            return Result.Failure<AdminRefundPaymentResult>(refundCreateResult.Error);
        }

        var refundIntent = refundCreateResult.Value;

        // C. 檢查 ProviderTransactionId 是否有效
        if (string.IsNullOrWhiteSpace(targetAttempt.ProviderTransactionId))
        {
            return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.MissingProviderTransactionId);
        }

        // D. 構建提供者請求並呼叫 CheckExecutionReadinessAsync
        var executionRequest = new RefundExecutionRequest(
            targetAttempt.Id,
            targetAttempt.ProviderTransactionId,
            targetAttempt.ProviderAuthorizationReference,
            targetAttempt.Amount.Amount,
            targetAttempt.Amount.Currency);

        var readinessResult = await _refundProvider.CheckExecutionReadinessAsync(executionRequest, cancellationToken);

        switch (readinessResult.Outcome)
        {
            case RefundReadinessOutcome.ExecutionTemporarilyBlocked:
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.ExecutionTemporarilyBlocked,
                    null));

            case RefundReadinessOutcome.ManualProviderResolutionRequired:
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.ManualProviderResolutionRequired,
                    null));

            case RefundReadinessOutcome.Unresolved:
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.Unresolved,
                    null));

            case RefundReadinessOutcome.AlreadyRefunded:
                // 金流商已退款但本地尚無紀錄：建立持久審計意圖並標記為 Succeeded
                bool createdForHistorical = await _paymentRefundRepository.TryCreateIntentAsync(refundIntent, cancellationToken);
                if (!createdForHistorical)
                {
                    return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.RefundAlreadyExists);
                }

                refundIntent.MarkAsSucceeded(_timeProvider.GetUtcNow());
                await _paymentRefundRepository.SaveChangesAsync(cancellationToken);

                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.AlreadyRefunded,
                    PaymentRefundStatus.Succeeded));

            case RefundReadinessOutcome.Ready:
                // 原子化建立持久化退款意圖（序列化點）
                bool created = await _paymentRefundRepository.TryCreateIntentAsync(refundIntent, cancellationToken);
                if (!created)
                {
                    // 競態失敗者：不呼叫 ExecuteRefundAsync，回傳衝突
                    return Result.Failure<AdminRefundPaymentResult>(PaymentRefundErrors.RefundAlreadyExists);
                }

                // 唯有持久意圖創建成功之請求，方得呼叫 ExecuteRefundAsync
                var execResult = await _refundProvider.ExecuteRefundAsync(executionRequest, cancellationToken);

                switch (execResult.Outcome)
                {
                    case RefundExecutionOutcome.RefundCompleted:
                        refundIntent.MarkAsSucceeded(_timeProvider.GetUtcNow());
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.RefundCompleted,
                            PaymentRefundStatus.Succeeded));

                    case RefundExecutionOutcome.AlreadyRefunded:
                        refundIntent.MarkAsSucceeded(_timeProvider.GetUtcNow());
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.AlreadyRefunded,
                            PaymentRefundStatus.Succeeded));

                    case RefundExecutionOutcome.Unresolved:
                        refundIntent.MarkAsUnresolved();
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.Unresolved,
                            PaymentRefundStatus.Unresolved));

                    case RefundExecutionOutcome.ManualProviderResolutionRequired:
                        refundIntent.MarkAsUnresolved();
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.ManualProviderResolutionRequired,
                            PaymentRefundStatus.Unresolved));

                    case RefundExecutionOutcome.ExecutionTemporarilyBlocked:
                        refundIntent.MarkAsUnresolved();
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.ExecutionTemporarilyBlocked,
                            PaymentRefundStatus.Unresolved));

                    default:
                        refundIntent.MarkAsUnresolved();
                        await _paymentRefundRepository.SaveChangesAsync(cancellationToken);
                        return Result.Success(new AdminRefundPaymentResult(
                            targetAttempt.Id.Value,
                            AdminRefundOutcome.Unresolved,
                            PaymentRefundStatus.Unresolved));
                }

            default:
                return Result.Success(new AdminRefundPaymentResult(
                    targetAttempt.Id.Value,
                    AdminRefundOutcome.Unresolved,
                    null));
        }
    }
}
