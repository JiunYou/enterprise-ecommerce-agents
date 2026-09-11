using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetOrderById;

public class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IPaymentAttemptRepository> _paymentAttemptRepositoryMock;
    private readonly Mock<IPaymentRefundRepository> _paymentRefundRepositoryMock;
    private readonly GetOrderByIdQueryHandler _handler;

    public GetOrderByIdQueryHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _paymentAttemptRepositoryMock = new Mock<IPaymentAttemptRepository>();
        _paymentRefundRepositoryMock = new Mock<IPaymentRefundRepository>();
        _handler = new GetOrderByIdQueryHandler(
            _orderRepositoryMock.Object,
            _paymentAttemptRepositoryMock.Object,
            _paymentRefundRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderExists_ShouldReturnSuccessWithOrderResponse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var productId = new ProductId(Guid.NewGuid());
        var unitPrice = new Money(100m, "TWD");
        order.AddItem(productId, unitPrice, 2);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(order.Id.Value, order.CustomerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(order.Id.Value, result.Value.Id);
        Assert.Equal(customerId, result.Value.CustomerId);
        Assert.Equal("TWD", result.Value.Currency);
        Assert.Equal(200m, result.Value.TotalAmount);
        Assert.Single(result.Value.Items);

        var item = result.Value.Items.First();
        Assert.Equal(productId.Value, item.ProductId);
        Assert.Equal(100m, item.UnitPrice);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(200m, item.TotalPrice);
    }

    [Fact]
    public async Task Handle_WhenOrderDoesNotExist_ShouldReturnFailureWithNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var query = new GetOrderByIdQuery(orderId, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
    }
    [Fact]
    public async Task Handle_WhenCustomerMismatch_ShouldReturnNotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var differentCustomerId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(order.Id.Value, differentCustomerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenOrderIsShippedWithCompleteTracking_ReturnsShipmentTrackingResponse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        var shipping = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "St 1").Value;
        order.Submit(shipping, DateTimeOffset.UtcNow.AddMinutes(-10));
        order.MarkAsPaid();

        var shippedAt = new DateTimeOffset(2026, 9, 11, 15, 0, 0, TimeSpan.Zero);
        order.Ship("HCT Logistics", "HCT-9999", shippedAt);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ShipmentTracking);
        Assert.Equal("HCT Logistics", result.Value.ShipmentTracking!.Carrier);
        Assert.Equal("HCT-9999", result.Value.ShipmentTracking.TrackingNumber);
        Assert.Equal(shippedAt, result.Value.ShipmentTracking.ShippedAt);
    }

    [Fact]
    public async Task Handle_WhenOrderIsHistoricalShippedWithoutTracking_ReturnsNullShipmentTracking()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100m, "TWD"), 1);
        order.ChangeStatus(OrderStatus.Submitted);
        order.ChangeStatus(OrderStatus.Paid);
        order.Ship(); // Historical ship without tracking fields

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Shipped", result.Value.Status);
        Assert.Null(result.Value.ShipmentTracking);
    }

    [Fact]
    public async Task Handle_WhenNoRefundRequiredAttempts_ShouldReturnEmptyRefundsAndNotQueryRefundRepository()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var attempt = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attempt.MarkAsSucceeded("TX123", DateTimeOffset.UtcNow);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Refunds);
        Assert.Empty(result.Value.Refunds);
        _paymentRefundRepositoryMock.Verify(
            r => r.GetByOrderIdAsync(It.IsAny<OrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRefundRequiredWithoutPaymentRefund_ShouldReturnRequiredStatusWithNullTimestamps()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var attempt = PaymentAttempt.Create(order.Id, new Money(250m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attempt.MarkAsRefundRequired("TX123", DateTimeOffset.UtcNow, "AUTH123");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund>());

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Refunds);
        var refund = result.Value.Refunds.First();
        Assert.Equal(250m, refund.Amount);
        Assert.Equal("TWD", refund.Currency);
        Assert.Equal("Required", refund.Status);
        Assert.Null(refund.RequestedAt);
        Assert.Null(refund.CompletedAt);
    }

    [Fact]
    public async Task Handle_WhenPendingRefund_ShouldReturnProcessingStatusWithRequestedAt()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var attempt = PaymentAttempt.Create(order.Id, new Money(300m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attempt.MarkAsRefundRequired("TX123", DateTimeOffset.UtcNow, "AUTH123");

        var requestedAt = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var refund = PaymentRefund.Create(attempt.Id, "Customer requested", "iss", "sub", requestedAt).Value;

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund> { refund });

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Refunds);
        var item = result.Value.Refunds.First();
        Assert.Equal(300m, item.Amount);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal("Processing", item.Status);
        Assert.Equal(requestedAt, item.RequestedAt);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public async Task Handle_WhenSucceededRefund_ShouldReturnSucceededStatusWithTimestamps()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var attempt = PaymentAttempt.Create(order.Id, new Money(500m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attempt.MarkAsRefundRequired("TX123", DateTimeOffset.UtcNow, "AUTH123");

        var requestedAt = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var completedAt = new DateTimeOffset(2026, 9, 11, 10, 30, 0, TimeSpan.Zero);
        var refund = PaymentRefund.Create(attempt.Id, "Customer requested", "iss", "sub", requestedAt).Value;
        refund.MarkAsSucceeded(completedAt);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund> { refund });

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Refunds);
        var item = result.Value.Refunds.First();
        Assert.Equal(500m, item.Amount);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal("Succeeded", item.Status);
        Assert.Equal(requestedAt, item.RequestedAt);
        Assert.Equal(completedAt, item.CompletedAt);
    }

    [Fact]
    public async Task Handle_WhenFailedRefund_ShouldReturnNeedsReviewStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var attempt = PaymentAttempt.Create(order.Id, new Money(400m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attempt.MarkAsRefundRequired("TX123", DateTimeOffset.UtcNow, "AUTH123");

        var requestedAt = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var completedAt = new DateTimeOffset(2026, 9, 11, 10, 15, 0, TimeSpan.Zero);
        var refund = PaymentRefund.Create(attempt.Id, "Customer requested", "iss", "sub", requestedAt).Value;
        refund.MarkAsFailed(completedAt);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund> { refund });

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Refunds);
        var item = result.Value.Refunds.First();
        Assert.Equal(400m, item.Amount);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal("NeedsReview", item.Status);
        Assert.Equal(requestedAt, item.RequestedAt);
        Assert.Equal(completedAt, item.CompletedAt);
    }

    [Fact]
    public async Task Handle_WhenUnresolvedRefund_ShouldReturnNeedsReviewStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var attempt = PaymentAttempt.Create(order.Id, new Money(450m, "TWD"), "ECPay", Guid.NewGuid(), DateTimeOffset.UtcNow);
        attempt.MarkAsRefundRequired("TX123", DateTimeOffset.UtcNow, "AUTH123");

        var requestedAt = new DateTimeOffset(2026, 9, 11, 10, 0, 0, TimeSpan.Zero);
        var refund = PaymentRefund.Create(attempt.Id, "Customer requested", "iss", "sub", requestedAt).Value;
        refund.MarkAsUnresolved();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund> { refund });

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Refunds);
        var item = result.Value.Refunds.First();
        Assert.Equal(450m, item.Amount);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal("NeedsReview", item.Status);
        Assert.Equal(requestedAt, item.RequestedAt);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public async Task Handle_WhenMultipleRefundRequiredAttempts_ShouldReturnDeterministicOrderAndCorrelateIndependently()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var baseTime = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

        var attempt1 = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), baseTime.AddMinutes(-20));
        attempt1.MarkAsRefundRequired("TX1", baseTime.AddMinutes(-18), "AUTH1");

        var attempt2 = PaymentAttempt.Create(order.Id, new Money(200m, "TWD"), "ECPay", Guid.NewGuid(), baseTime.AddMinutes(-10));
        attempt2.MarkAsRefundRequired("TX2", baseTime.AddMinutes(-8), "AUTH2");

        var refund2 = PaymentRefund.Create(attempt2.Id, "Reason", "iss", "sub", baseTime.AddMinutes(-5)).Value;
        refund2.MarkAsSucceeded(baseTime.AddMinutes(-2));

        // 故意以相反順序放入以驗證排序性
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { attempt2, attempt1 });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund> { refund2 });

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Refunds.Count);

        var first = result.Value.Refunds[0];
        Assert.Equal(100m, first.Amount);
        Assert.Equal("Required", first.Status);

        var second = result.Value.Refunds[1];
        Assert.Equal(200m, second.Amount);
        Assert.Equal("Succeeded", second.Status);
    }

    [Fact]
    public async Task Handle_WhenNonRefundRequiredAttemptsExist_ShouldIgnoreThem()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        var baseTime = DateTimeOffset.UtcNow;

        var failedAttempt = PaymentAttempt.Create(order.Id, new Money(50m, "TWD"), "ECPay", Guid.NewGuid(), baseTime.AddMinutes(-30));
        failedAttempt.MarkAsFailed("TX0", baseTime.AddMinutes(-28));

        var succeededAttempt = PaymentAttempt.Create(order.Id, new Money(60m, "TWD"), "ECPay", Guid.NewGuid(), baseTime.AddMinutes(-25));
        succeededAttempt.MarkAsSucceeded("TX1", baseTime.AddMinutes(-24));

        var pendingAttempt = PaymentAttempt.Create(order.Id, new Money(70m, "TWD"), "ECPay", Guid.NewGuid(), baseTime.AddMinutes(-20));

        var refundRequiredAttempt = PaymentAttempt.Create(order.Id, new Money(80m, "TWD"), "ECPay", Guid.NewGuid(), baseTime.AddMinutes(-10));
        refundRequiredAttempt.MarkAsRefundRequired("TX3", baseTime.AddMinutes(-9), "AUTH3");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { failedAttempt, succeededAttempt, pendingAttempt, refundRequiredAttempt });
        _paymentRefundRepositoryMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund>());

        var query = new GetOrderByIdQuery(order.Id.Value, customerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Refunds);
        Assert.Equal(80m, result.Value.Refunds.First().Amount);
    }

    [Fact]
    public async Task Handle_WhenCrossCustomerOrder_ShouldReturnNotFoundAndNeverQueryPaymentOrRefundRepositories()
    {
        // Arrange
        var ownerCustomerId = Guid.NewGuid();
        var order = Order.Create(ownerCustomerId, "TWD");

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var attackerCustomerId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(order.Id.Value, attackerCustomerId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);

        _paymentAttemptRepositoryMock.Verify(
            r => r.GetByOrderIdAsync(It.IsAny<OrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _paymentRefundRepositoryMock.Verify(
            r => r.GetByOrderIdAsync(It.IsAny<OrderId>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
