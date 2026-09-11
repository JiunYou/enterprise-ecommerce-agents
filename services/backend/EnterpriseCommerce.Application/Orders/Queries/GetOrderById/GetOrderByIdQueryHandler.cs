using EnterpriseCommerce.Application.Common.CQRS;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Application.Orders.Queries.GetOrderById;

internal sealed class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPaymentAttemptRepository _paymentAttemptRepository;
    private readonly IPaymentRefundRepository _paymentRefundRepository;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IPaymentAttemptRepository paymentAttemptRepository,
        IPaymentRefundRepository paymentRefundRepository)
    {
        _orderRepository = orderRepository;
        _paymentAttemptRepository = paymentAttemptRepository;
        _paymentRefundRepository = paymentRefundRepository;
    }

    public async Task<Result<OrderResponse>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var orderId = new OrderId(request.OrderId);
        var order = await _orderRepository.GetByIdAsync(orderId, cancellationToken);

        // 擁有權/IDOR 邊界保護：若不存在或顧客不符，立即回傳 NotFound，絕不查詢支付或退款儲存庫
        if (order is null || order.CustomerId != request.CustomerId)
        {
            return Result.Failure<OrderResponse>(OrderErrors.NotFound);
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

        ShipmentTrackingResponse? shipmentTracking =
            order.ShippingCarrier is not null &&
            order.ShippingTrackingNumber is not null &&
            order.ShippedAt is not null
                ? new ShipmentTrackingResponse(
                    order.ShippingCarrier,
                    order.ShippingTrackingNumber,
                    order.ShippedAt.Value)
                : null;

        // 查詢支付嘗試
        var allAttempts = await _paymentAttemptRepository.GetByOrderIdAsync(order.Id, cancellationToken) ?? Array.Empty<PaymentAttempt>();
        var refundRequiredAttempts = allAttempts
            .Where(a => a.Status == PaymentAttemptStatus.RefundRequired)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id.Value)
            .ToList();

        IReadOnlyList<CustomerRefundStatusResponse> refundResponses;

        if (refundRequiredAttempts.Count == 0)
        {
            // 最佳化：無 RefundRequired 時，略過退款儲存庫查詢
            refundResponses = Array.Empty<CustomerRefundStatusResponse>();
        }
        else
        {
            var refunds = await _paymentRefundRepository.GetByOrderIdAsync(order.Id, cancellationToken) ?? Array.Empty<PaymentRefund>();
            var refundsByAttemptId = refunds.ToDictionary(r => r.Id);

            var mappedList = new List<CustomerRefundStatusResponse>(refundRequiredAttempts.Count);
            foreach (var attempt in refundRequiredAttempts)
            {
                string status;
                DateTimeOffset? requestedAt = null;
                DateTimeOffset? completedAt = null;

                if (refundsByAttemptId.TryGetValue(attempt.Id, out var existingRefund))
                {
                    requestedAt = existingRefund.RequestedAt;
                    completedAt = existingRefund.CompletedAt;
                    status = existingRefund.Status switch
                    {
                        PaymentRefundStatus.Pending => "Processing",
                        PaymentRefundStatus.Succeeded => "Succeeded",
                        PaymentRefundStatus.Failed => "NeedsReview",
                        PaymentRefundStatus.Unresolved => "NeedsReview",
                        _ => "NeedsReview"
                    };
                }
                else
                {
                    status = "Required";
                    requestedAt = null;
                    completedAt = null;
                }

                mappedList.Add(new CustomerRefundStatusResponse(
                    attempt.Amount.Amount,
                    attempt.Amount.Currency,
                    status,
                    requestedAt,
                    completedAt));
            }

            refundResponses = mappedList;
        }

        var response = new OrderResponse(
            order.Id.Value,
            order.CustomerId,
            order.Status.ToString(),
            order.Currency,
            order.TotalAmount.Amount,
            items,
            shippingAddress,
            shipmentTracking,
            refundResponses);

        return Result.Success(response);
    }
}
