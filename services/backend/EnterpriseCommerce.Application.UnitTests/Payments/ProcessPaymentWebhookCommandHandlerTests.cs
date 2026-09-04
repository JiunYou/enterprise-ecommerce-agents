using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Payments;

public class ProcessPaymentWebhookCommandHandlerTests
{
    private readonly Mock<IPaymentAttemptRepository> _paymentAttemptRepositoryMock;
    private readonly Mock<IPaymentWebhookReceiptRepository> _webhookReceiptRepositoryMock;
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;
    private readonly ProcessPaymentWebhookCommandHandler _handler;

    public ProcessPaymentWebhookCommandHandlerTests()
    {
        _paymentAttemptRepositoryMock = new Mock<IPaymentAttemptRepository>();
        _webhookReceiptRepositoryMock = new Mock<IPaymentWebhookReceiptRepository>();
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _timeProvider = TimeProvider.System;
        _configuration = new Mock<IConfiguration>().Object;

        _handler = new ProcessPaymentWebhookCommandHandler(
            _paymentAttemptRepositoryMock.Object,
            _webhookReceiptRepositoryMock.Object,
            _orderRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider,
            _configuration);
    }

    [Fact]
    public async Task Handle_WhenProviderMismatches_FailsWithProviderMismatchAndRollsBack()
    {
        // Arrange: PaymentAttempt is created with Provider = "Stripe"
        var orderId = new OrderId(Guid.NewGuid());
        var attempt = PaymentAttempt.Create(
            orderId,
            new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"),
            "Stripe",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        _webhookReceiptRepositoryMock.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _paymentAttemptRepositoryMock.Setup(r => r.GetByIdAsync(attempt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt);

        // Act: Webhook received claiming to be from "DifferentProvider"
        var command = new ProcessPaymentWebhookCommand(
            attempt.Id.Value,
            "DifferentProvider",
            "evt_123",
            "txn_123",
            100m,
            "USD",
            true);

        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(PaymentErrors.ProviderMismatch.Code);

        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenReceiptAlreadyExists_ReturnsSuccessIdempotentlyWithoutStateMutation()
    {
        // Arrange
        _webhookReceiptRepositoryMock.Setup(r => r.ExistsAsync("Stripe", "evt_already_received", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new ProcessPaymentWebhookCommand(
            Guid.NewGuid(),
            "Stripe",
            "evt_already_received",
            "txn_123",
            100m,
            "USD",
            true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _paymentAttemptRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<PaymentAttemptId>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
