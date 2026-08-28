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

            // Check if there is an active pending attempt
            var pendingAttempt = await _paymentAttemptRepository.GetActivePendingAttemptAsync(orderId, cancellationToken);
            if (pendingAttempt != null)
            {
                // Idempotency check: if the active attempt has the same idempotency key, we might return it?
                // Wait, if it has the SAME idempotency key, should we return the existing URL?
                // The MVP proposal says: "Idempotency identity/key: PaymentAttempts(OrderId, IdempotencyKey) UNIQUE"
                // "Behavior on repeated request: returns the existing pending Attempt URL/Secret without creating a new one or charging again."
                if (pendingAttempt.IdempotencyKey == request.IdempotencyKey)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    
                    // Call provider again with the SAME PaymentAttemptId.
                    // An idempotent provider will return the same ProviderTransactionId and URL.
                    var existingProviderResponse = await _paymentProvider.InitiatePaymentAsync(
                        pendingAttempt.Id,
                        orderId,
                        order.TotalAmount.Amount,
                        order.TotalAmount.Currency,
                        cancellationToken);

                    return Result.Success(existingProviderResponse);
                }

                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<InitiatePaymentResponse>(PaymentErrors.ConcurrentInitiation);
            }

            // Create new pending attempt in DB (BEFORE provider call)
            var attempt = PaymentAttempt.Create(
                orderId,
                order.TotalAmount,
                "DummyProvider",
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
