using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Queries.GetAdminOrderById;

public class AdminOrderDetailRefundReadModelTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock;
    private readonly Mock<IAdminOrderCancellationStore> _cancellationStoreMock;
    private readonly Mock<IPaymentAttemptRepository> _paymentAttemptRepoMock;
    private readonly Mock<IPaymentRefundRepository> _paymentRefundRepoMock;
    private readonly IConfiguration _configuration;
    private readonly DateTimeOffset _fixedTime = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private readonly GetAdminOrderByIdQueryHandler _handler;

    public AdminOrderDetailRefundReadModelTests()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _cancellationStoreMock = new Mock<IAdminOrderCancellationStore>();
        _paymentAttemptRepoMock = new Mock<IPaymentAttemptRepository>();
        _paymentRefundRepoMock = new Mock<IPaymentRefundRepository>();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["BackgroundJobs:ExpiredOrdersCleanup:ExpirationWindowMinutes"]).Returns("15");
        _configuration = configMock.Object;

        var timeProviderMock = new Mock<TimeProvider>();
        timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _handler = new GetAdminOrderByIdQueryHandler(
            _orderRepoMock.Object,
            _cancellationStoreMock.Object,
            _paymentAttemptRepoMock.Object,
            _paymentRefundRepoMock.Object,
            _configuration,
            timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_OnlyRefundRequiredAttemptsIncluded_NonRefundRequiredExcluded()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        var refundRequiredAttempt = PaymentAttempt.Create(
            order.Id,
            new Money(150m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            _fixedTime.AddMinutes(-30),
            "AUTH_123");
        refundRequiredAttempt.MarkAsRefundRequired("TX_1", _fixedTime.AddMinutes(-20), "AUTH_123");

        var pendingAttempt = PaymentAttempt.Create(
            order.Id,
            new Money(200m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            _fixedTime.AddMinutes(-30),
            "AUTH_234");

        var failedAttempt = PaymentAttempt.Create(
            order.Id,
            new Money(250m, "TWD"),
            "ECPay",
            Guid.NewGuid(),
            _fixedTime.AddMinutes(-30),
            "AUTH_345");
        failedAttempt.MarkAsFailed("TX_3", _fixedTime.AddMinutes(-20));

        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentAttempt> { refundRequiredAttempt, pendingAttempt, failedAttempt });
        _paymentRefundRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentRefund>());

        // Act
        var result = await _handler.Handle(new GetAdminOrderByIdQuery(order.Id.Value), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Single(detail.RefundRequiredPayments);

        var item = detail.RefundRequiredPayments.First();
        Assert.Equal(refundRequiredAttempt.Id.Value, item.PaymentAttemptId);
        Assert.Equal(150m, item.Amount);
        Assert.Equal("TWD", item.Currency);
        Assert.Equal("Eligible", item.RefundCapability);
        Assert.Null(item.RefundStatus);

        // Ensure sensitive provider fields are absent from DTO type definition
        var itemType = typeof(AdminRefundRequiredPaymentResponse);
        Assert.Null(itemType.GetProperty("ProviderTransactionId"));
        Assert.Null(itemType.GetProperty("ProviderAuthorizationReference"));
        Assert.Null(itemType.GetProperty("CheckMacValue"));
        Assert.Null(itemType.GetProperty("MerchantTradeNo"));
    }

    [Fact]
    public async Task Handle_ExistingSucceededRefund_MapsToCompleted()
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        var attempt = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), _fixedTime.AddMinutes(-30), "AUTH_123");
        attempt.MarkAsRefundRequired("TX_1", _fixedTime.AddMinutes(-20), "AUTH_123");

        var refund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime.AddMinutes(-10)).Value;
        refund.MarkAsSucceeded(_fixedTime.AddMinutes(-5));

        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentRefund> { refund });

        var result = await _handler.Handle(new GetAdminOrderByIdQuery(order.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.RefundRequiredPayments);
        Assert.Equal("Completed", item.RefundCapability);
        Assert.Equal("Succeeded", item.RefundStatus);
    }

    [Fact]
    public async Task Handle_ExistingFailedRefund_MapsToManualProviderResolutionRequired()
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        var attempt = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), _fixedTime.AddMinutes(-30), "AUTH_123");
        attempt.MarkAsRefundRequired("TX_1", _fixedTime.AddMinutes(-20), "AUTH_123");

        var refund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime.AddMinutes(-10)).Value;
        refund.MarkAsFailed(_fixedTime.AddMinutes(-5));

        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentRefund> { refund });

        var result = await _handler.Handle(new GetAdminOrderByIdQuery(order.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.RefundRequiredPayments);
        Assert.Equal("ManualProviderResolutionRequired", item.RefundCapability);
        Assert.Equal("Failed", item.RefundStatus);
    }

    [Theory]
    [InlineData(PaymentRefundStatus.Pending)]
    [InlineData(PaymentRefundStatus.Unresolved)]
    public async Task Handle_ExistingPendingOrUnresolved_MapsToReconciliationOnly(PaymentRefundStatus status)
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        var attempt = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), _fixedTime.AddMinutes(-30), "AUTH_123");
        attempt.MarkAsRefundRequired("TX_1", _fixedTime.AddMinutes(-20), "AUTH_123");

        var refund = PaymentRefund.Create(attempt.Id, "Reason", "iss", "sub", _fixedTime.AddMinutes(-10)).Value;
        if (status == PaymentRefundStatus.Unresolved)
        {
            refund.MarkAsUnresolved();
        }

        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentRefund> { refund });

        var result = await _handler.Handle(new GetAdminOrderByIdQuery(order.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.RefundRequiredPayments);
        Assert.Equal("ReconciliationOnly", item.RefundCapability);
        Assert.Equal(status.ToString(), item.RefundStatus);
    }

    [Fact]
    public async Task Handle_NoRefund_MissingAuthRef_MapsToManualProviderResolutionRequired()
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.Cancel();

        var attempt = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), _fixedTime.AddMinutes(-30), null);
        attempt.MarkAsRefundRequired("TX_1", _fixedTime.AddMinutes(-20), null);

        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentRefund>());

        var result = await _handler.Handle(new GetAdminOrderByIdQuery(order.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.RefundRequiredPayments);
        Assert.Equal("ManualProviderResolutionRequired", item.RefundCapability);
        Assert.Null(item.RefundStatus);
    }

    [Fact]
    public async Task Handle_NoRefund_LocallyInvalidSubmittedNotExpired_MapsToNotEligible()
    {
        var order = Order.Create(Guid.NewGuid(), "TWD");
        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "City", "Line1", null).Value;
        order.Submit(address, _fixedTime.AddMinutes(-5)); // Submitted 5 min ago, expiration is 15 min => not expired

        var attempt = PaymentAttempt.Create(order.Id, new Money(100m, "TWD"), "ECPay", Guid.NewGuid(), _fixedTime.AddMinutes(-30), "AUTH_123");
        attempt.MarkAsRefundRequired("TX_1", _fixedTime.AddMinutes(-20), "AUTH_123");

        _orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        _paymentAttemptRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentAttempt> { attempt });
        _paymentRefundRepoMock.Setup(r => r.GetByOrderIdAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<PaymentRefund>());

        var result = await _handler.Handle(new GetAdminOrderByIdQuery(order.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.RefundRequiredPayments);
        Assert.Equal("NotEligible", item.RefundCapability);
        Assert.Null(item.RefundStatus);
    }
}
