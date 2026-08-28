using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.Configuration;

namespace EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;

internal sealed class ProcessPaymentWebhookCommandHandler : ICommandHandler<ProcessPaymentWebhookCommand>
{
    private readonly IPaymentAttemptRepository _paymentAttemptRepository;
    private readonly IPaymentWebhookReceiptRepository _webhookReceiptRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;

    public ProcessPaymentWebhookCommandHandler(
        IPaymentAttemptRepository paymentAttemptRepository,
        IPaymentWebhookReceiptRepository webhookReceiptRepository,
        IOrderRepository orderRepository,
        IApplicationUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _paymentAttemptRepository = paymentAttemptRepository;
        _webhookReceiptRepository = webhookReceiptRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    public async Task<Result> Handle(ProcessPaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var exists = await _webhookReceiptRepository.ExistsAsync(request.Provider, request.ProviderEventId, cancellationToken);
            if (exists)
            {
                // Webhook already processed. Acknowledge idempotently.
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Success();
            }

            var attemptId = new PaymentAttemptId(request.PaymentAttemptId);
            var receipt = PaymentWebhookReceipt.Create(
                request.Provider,
                request.ProviderEventId,
                attemptId,
                _timeProvider.GetUtcNow());

            _webhookReceiptRepository.Add(receipt);

            var attempt = await _paymentAttemptRepository.GetByIdAsync(attemptId, cancellationToken);
            if (attempt is null)
            {
                // Attempt not found. We might still record the receipt but for MVP we fail the process.
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(PaymentErrors.NotFound);
            }

            if (attempt.Status != PaymentAttemptStatus.Pending)
            {
                // If it's not pending, it's already finalized. 
                // We acknowledge idempotently but don't change state.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return Result.Success();
            }

            // Verify integrity
            if (attempt.Amount.Amount != request.Amount)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(PaymentErrors.AmountMismatch);
            }

            if (attempt.Amount.Currency != request.Currency)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(PaymentErrors.CurrencyMismatch);
            }

            var order = await _orderRepository.GetByIdAsync(attempt.OrderId, cancellationToken);
            if (order is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(OrderErrors.NotFound);
            }

            if (!request.IsSuccess)
            {
                attempt.MarkAsFailed(request.ProviderTransactionId, _timeProvider.GetUtcNow());
            }
            else
            {
                var expirationStr = _configuration["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"];
                var expirationMinutes = int.TryParse(expirationStr, out var e) ? e : 15;
                var threshold = _timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(expirationMinutes));

                // If Order is already Cancelled or it's Expired based on threshold
                if (order.Status != OrderStatus.Submitted || order.IsExpired(threshold))
                {
                    // Refund Required path
                    attempt.MarkAsRefundRequired(request.ProviderTransactionId, _timeProvider.GetUtcNow());
                }
                else
                {
                    // Success path
                    attempt.MarkAsSucceeded(request.ProviderTransactionId, _timeProvider.GetUtcNow());

                    var markPaidResult = order.MarkAsPaid();
                    if (markPaidResult.IsFailure)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return markPaidResult;
                    }
                }
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            
            var typeName = ex.GetType().Name;
            if (typeName == "DbUpdateConcurrencyException")
            {
                var alreadyProcessed = await _webhookReceiptRepository.ExistsAsync(request.Provider, request.ProviderEventId, cancellationToken);
                if (alreadyProcessed)
                {
                    return Result.Success();
                }

                throw;
            }

            if (typeName == "DbUpdateException")
            {
                if (ex.InnerException?.Message.Contains("IX_PaymentWebhookReceipts_Provider_ProviderEventId") == true)
                {
                    // Handle concurrent duplicate webhook delivery
                    // The exact same event was just committed by another thread.
                    // Acknowledge idempotently.
                    return Result.Success();
                }

                if (ex.InnerException?.Message.Contains("IX_PaymentAttempts_Provider_ProviderTransactionId") == true)
                {
                    // Handle semantic duplicate
                    var existingAttempt = await _paymentAttemptRepository.GetByProviderTransactionIdAsync(request.Provider, request.ProviderTransactionId, cancellationToken);
                    
                    if (existingAttempt != null 
                        && existingAttempt.Id.Value == request.PaymentAttemptId
                        && existingAttempt.Amount.Amount == request.Amount 
                        && existingAttempt.Amount.Currency == request.Currency
                        && existingAttempt.Status != PaymentAttemptStatus.Pending)
                    {
                        return Result.Success();
                    }

                    return Result.Failure(PaymentErrors.DuplicateTransactionIdMismatch);
                }
            }

            throw;
        }
    }
}
