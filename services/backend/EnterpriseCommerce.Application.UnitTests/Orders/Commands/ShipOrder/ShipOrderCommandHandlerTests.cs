using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.ShipOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.ShipOrder;

public class ShipOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly ShipOrderCommandHandler _handler;

    public ShipOrderCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 11, 14, 0, 0, TimeSpan.Zero));
        _handler = new ShipOrderCommandHandler(_orderRepositoryMock.Object, _unitOfWorkMock.Object, _timeProviderMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPaidAndHasAddress_ShouldShipWithAuthoritativeTimeAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        var shippingAddress = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "St 1").Value;
        order.Submit(shippingAddress, DateTimeOffset.UtcNow.AddMinutes(-10));
        order.MarkAsPaid();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var expectedShippedAt = new DateTimeOffset(2026, 9, 11, 14, 0, 0, TimeSpan.Zero);
        var command = new ShipOrderCommand(order.Id.Value, "Black Cat Express", "TRACK-12345");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("Black Cat Express", order.ShippingCarrier);
        Assert.Equal("TRACK-12345", order.ShippingTrackingNumber);
        Assert.Equal(expectedShippedAt, order.ShippedAt);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPending_ShouldReturnInvalidStatusTransitionAndNotSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new ShipOrderCommand(order.Id.Value, "Black Cat", "TRACK-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidStatusTransition.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenShippingAddressMissing_ShouldReturnShippingAddressRequiredAndNotSave()
    {
        // Arrange: construct a Paid order without shipping address
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        order.ChangeStatus(OrderStatus.Submitted);
        order.ChangeStatus(OrderStatus.Paid);

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var command = new ShipOrderCommand(order.Id.Value, "Black Cat", "TRACK-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ShippingAddressRequired.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new ShipOrderCommand(orderId, "Black Cat", "TRACK-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenConcurrencyExceptionOccurs_ShouldReturnConcurrencyConflict()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        var shippingAddress = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "St 1").Value;
        order.Submit(shippingAddress, DateTimeOffset.UtcNow.AddMinutes(-10));
        order.MarkAsPaid();

        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var command = new ShipOrderCommand(order.Id.Value, "Black Cat", "TRACK-123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Order.ConcurrencyConflict", result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class DbUpdateConcurrencyException : Exception
    {
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "Carrier", "TRACK-1", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", "", "TRACK-1", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", "   ", "TRACK-1", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", "Carrier", "", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", "Carrier", "   ", false)]
    [InlineData("11111111-1111-1111-1111-111111111111", "Carrier", "TRACK-1", true)]
    public void Validator_ShouldValidateRequiredFields(string orderIdStr, string carrier, string trackingNumber, bool expectedValid)
    {
        // Arrange
        var validator = new ShipOrderCommandValidator();
        var command = new ShipOrderCommand(Guid.Parse(orderIdStr), carrier, trackingNumber);

        // Act
        var validationResult = validator.Validate(command);

        // Assert
        Assert.Equal(expectedValid, validationResult.IsValid);
    }

    [Fact]
    public void Validator_ShouldFail_WhenCarrierOrTrackingExceeds100Characters()
    {
        // Arrange
        var validator = new ShipOrderCommandValidator();
        var longCarrier = new string('A', 101);
        var longTracking = new string('B', 101);

        // Act & Assert
        var command1 = new ShipOrderCommand(Guid.NewGuid(), longCarrier, "TRACK-1");
        Assert.False(validator.Validate(command1).IsValid);

        var command2 = new ShipOrderCommand(Guid.NewGuid(), "Carrier", longTracking);
        Assert.False(validator.Validate(command2).IsValid);
    }
}
