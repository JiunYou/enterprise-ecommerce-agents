using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.Configuration;

namespace EnterpriseCommerce.Application.Payments.Commands.InitiatePayment;

internal sealed class InitiatePaymentCommandHandler : ICommandHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentAttemptRepository _paymentAttemptRepository;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IApplicationUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public InitiatePaymentCommandHandler(
        IOrderRepository orderRepository,
        IPaymentAttemptRepository paymentAttemptRepository,
        IPaymentProvider paymentProvider,
        IApplicationUnitOfWork unitOfWork,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        _orderRepository = orderRepository;
        _paymentAttemptRepository = paymentAttemptRepository;
        _paymentProvider = paymentProvider;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<Result<InitiatePaymentResponse>> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);

        // Required transaction boundary for pessimistic locking as per MVP design
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Acquire pessimistic row-lock on Order via repository
            var order = await _orderRepository.GetByIdForUpdateAsync(orderId, cancellationToken);

            if (order is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<InitiatePaymentResponse>(OrderErrors.NotFound);
            }

            if (order.Status != OrderStatus.Submitted)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<InitiatePaymentResponse>(OrderErrors.InvalidStatusTransition); // Or OrderErrors.InvalidStatus
            }

            if (order.CustomerId != request.CustomerId)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<InitiatePaymentResponse>(OrderErrors.NotFound);
            }

            var expirationStr = _configuration["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"];
            var expirationMinutes = int.TryParse(expirationStr, out var e) ? e : 15;
            var threshold = _timeProvider.GetUtcNow().Subtract(TimeSpan.FromMinutes(expirationMinutes));

            // If order is expired, it cannot be paid.
            if (order.IsExpired(threshold))
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<InitiatePaymentResponse>(PaymentErrors.InvalidStatusTransition); // Expired
            }

            // Check if an attempt already exists for this specific OrderId and IdempotencyKey
            var existingAttempt = await _paymentAttemptRepository.GetByOrderIdAndIdempotencyKeyAsync(orderId, request.IdempotencyKey, cancellationToken);
            if (existingAttempt != null)
            {
                // Rollback transaction as we do not need to persist a new attempt or mutate state
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                if (existingAttempt.Status == PaymentAttemptStatus.Pending)
                {
                    // Same-key idempotency: reuse the active pending PaymentAttempt
                    var existingProviderResponse = await _paymentProvider.InitiatePaymentAsync(
                        existingAttempt.Id,
                        orderId,
                        order.TotalAmount.Amount,
                        order.TotalAmount.Currency,
                        existingAttempt.CreatedAt,
                        cancellationToken);

                    return Result.Success(existingProviderResponse);
                }

                // If the same idempotency key already mapped to a non-Pending attempt, do not re-initiate with the same key.
                return Result.Failure<InitiatePaymentResponse>(PaymentErrors.InvalidStatusTransition);
            }

            // Create new pending attempt in DB (BEFORE provider call)
            // Even if older PaymentAttempts for the same Order remain Pending, each distinct IdempotencyKey represents a new provider attempt.
            var attempt = PaymentAttempt.Create(
                orderId,
                order.TotalAmount,
                _paymentProvider.ProviderName,
                request.IdempotencyKey,
                _timeProvider.GetUtcNow());

            _paymentAttemptRepository.Add(attempt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // Call provider to get client secret/URL outside of database transaction
            var providerResponse = await _paymentProvider.InitiatePaymentAsync(
                attempt.Id,
                orderId,
                order.TotalAmount.Amount,
                order.TotalAmount.Currency,
                attempt.CreatedAt,
                cancellationToken);

            return Result.Success(providerResponse);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
