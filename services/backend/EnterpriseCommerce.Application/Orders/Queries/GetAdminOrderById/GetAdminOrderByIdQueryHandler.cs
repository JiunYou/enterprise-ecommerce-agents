using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.Configuration;

namespace EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;

internal sealed class GetAdminOrderByIdQueryHandler : IQueryHandler<GetAdminOrderByIdQuery, AdminOrderDetailResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAdminOrderCancellationStore _adminOrderCancellationStore;
    private readonly IPaymentAttemptRepository? _paymentAttemptRepository;
    private readonly IPaymentRefundRepository? _paymentRefundRepository;
    private readonly IConfiguration? _configuration;
    private readonly TimeProvider? _timeProvider;

    public GetAdminOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IAdminOrderCancellationStore adminOrderCancellationStore)
        : this(orderRepository, adminOrderCancellationStore, null, null, null, null)
    {
    }

    public GetAdminOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IAdminOrderCancellationStore adminOrderCancellationStore,
        IPaymentAttemptRepository? paymentAttemptRepository,
        IPaymentRefundRepository? paymentRefundRepository,
        IConfiguration? configuration,
        TimeProvider? timeProvider)
    {
        _orderRepository = orderRepository;
        _adminOrderCancellationStore = adminOrderCancellationStore;
        _paymentAttemptRepository = paymentAttemptRepository;
        _paymentRefundRepository = paymentRefundRepository;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminOrderDetailResponse>> Handle(GetAdminOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        if (order is null)
        {
            return Result.Failure<AdminOrderDetailResponse>(OrderErrors.NotFound);
        }

        var items = order.Items.Select(item => new OrderItemResponse(
            item.ProductId.Value,
            item.UnitPrice.Amount,
            item.UnitPrice.Currency,
            item.Quantity,
            item.GetTotalPrice().Amount)).ToList();

        ShippingAddressResponse? shippingAddress = order.ShippingAddress is not null
            ? new ShippingAddressResponse(
                order.ShippingAddress.RecipientName,
                order.ShippingAddress.Phone,
                order.ShippingAddress.CountryCode,
                order.ShippingAddress.PostalCode,
                order.ShippingAddress.City,
                order.ShippingAddress.AddressLine1,
                order.ShippingAddress.AddressLine2)
            : null;

        var audit = await _adminOrderCancellationStore.GetByOrderIdAsync(order.Id.Value, cancellationToken);
        AdminCancellationResponse? adminCancellation = audit is not null
            ? new AdminCancellationResponse(
                audit.ActorIssuer,
                audit.ActorSubject,
                audit.CancelledAt,
                audit.Reason)
            : null;

        List<AdminRefundRequiredPaymentResponse> refundRequiredPayments = [];
        if (_paymentAttemptRepository is not null && _paymentRefundRepository is not null)
        {
            var allAttempts = await _paymentAttemptRepository.GetByOrderIdAsync(order.Id, cancellationToken);
            var refundRequiredAttempts = allAttempts.Where(a => a.Status == PaymentAttemptStatus.RefundRequired).ToList();

            if (refundRequiredAttempts.Count > 0)
            {
                var refunds = await _paymentRefundRepository.GetByOrderIdAsync(order.Id, cancellationToken);
                var refundsByAttemptId = refunds.ToDictionary(r => r.Id);

                var expirationStr = _configuration?["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"];
                var expirationMinutes = int.TryParse(expirationStr, out var m) ? m : 15;
                var utcNow = _timeProvider?.GetUtcNow() ?? DateTimeOffset.UtcNow;

                foreach (var attempt in refundRequiredAttempts)
                {
                    string capability;
                    string? refundStatus = null;

                    if (refundsByAttemptId.TryGetValue(attempt.Id, out var existingRefund))
                    {
                        refundStatus = existingRefund.Status.ToString();
                        capability = existingRefund.Status switch
                        {
                            PaymentRefundStatus.Succeeded => "Completed",
                            PaymentRefundStatus.Failed => "ManualProviderResolutionRequired",
                            PaymentRefundStatus.Pending or PaymentRefundStatus.Unresolved => "ReconciliationOnly",
                            _ => "ReconciliationOnly"
                        };
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(attempt.ProviderAuthorizationReference))
                        {
                            capability = "ManualProviderResolutionRequired";
                        }
                        else
                        {
                            var eligibility = RefundEligibilityEvaluator.Evaluate(
                                attempt,
                                order,
                                allAttempts,
                                utcNow,
                                expirationMinutes);

                            capability = eligibility == RefundEligibilityStatus.Eligible
                                ? "Eligible"
                                : "NotEligible";
                        }
                    }

                    refundRequiredPayments.Add(new AdminRefundRequiredPaymentResponse(
                        attempt.Id.Value,
                        attempt.Amount.Amount,
                        attempt.Amount.Currency,
                        capability,
                        refundStatus));
                }
            }
        }

        ShipmentTrackingResponse? shipmentTracking =
            order.ShippingCarrier is not null &&
            order.ShippingTrackingNumber is not null &&
            order.ShippedAt is not null
                ? new ShipmentTrackingResponse(
                    order.ShippingCarrier,
                    order.ShippingTrackingNumber,
                    order.ShippedAt.Value)
                : null;

        var response = new AdminOrderDetailResponse(
            order.Id.Value,
            order.CustomerId,
            order.Status.ToString(),
            order.Currency,
            order.TotalAmount.Amount,
            order.SubmittedAt,
            items,
            shippingAddress,
            adminCancellation,
            refundRequiredPayments,
            shipmentTracking);

        return Result.Success(response);

    }
}
