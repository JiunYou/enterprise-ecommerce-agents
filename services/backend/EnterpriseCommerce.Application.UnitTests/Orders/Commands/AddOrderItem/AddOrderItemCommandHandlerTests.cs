using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.AddOrderItem;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders.Commands.AddOrderItem;

public class AddOrderItemCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IProductRepository> _productRepositoryMock;
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock;
    private readonly AddOrderItemCommandHandler _handler;

    public AddOrderItemCommandHandlerTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _productRepositoryMock = new Mock<IProductRepository>();
        _unitOfWorkMock = new Mock<IApplicationUnitOfWork>();
        _handler = new AddOrderItemCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderExistsAndItemValid_ShouldAddItemAndSave()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var product = Product.Create("Test Product", "SKU-1", 150m, "TWD").Value;
        _productRepositoryMock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = new AddOrderItemCommand(order.Id.Value, order.CustomerId, product.Id, 2);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Single(order.Items);
        Assert.Equal(300m, order.TotalAmount.Amount);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOrderNotFound_ShouldReturnFailure()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(new OrderId(orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var command = new AddOrderItemCommand(orderId, Guid.NewGuid(), Guid.NewGuid(), 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldReturnFailure()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var productId = Guid.NewGuid();
        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var command = new AddOrderItemCommand(order.Id.Value, order.CustomerId, productId, 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.NotFound.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenProductNotActive_ShouldReturnFailure()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var product = Product.Create("Test Product", "SKU-1", 150m, "TWD").Value;
        product.Deactivate();

        _productRepositoryMock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = new AddOrderItemCommand(order.Id.Value, order.CustomerId, product.Id, 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.NotActive.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCurrencyMismatch_ShouldReturnFailure()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var product = Product.Create("Test Product", "SKU-1", 100m, "USD").Value;
        _productRepositoryMock.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var command = new AddOrderItemCommand(order.Id.Value, order.CustomerId, product.Id, 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.CurrencyMismatch.Code, result.Error.Code);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task Handle_WhenCustomerMismatch_ShouldReturnNotFound()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "TWD");
        _orderRepositoryMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var differentCustomerId = Guid.NewGuid();
        var command = new AddOrderItemCommand(order.Id.Value, differentCustomerId, Guid.NewGuid(), 1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.NotFound.Code, result.Error.Code);
        Assert.Empty(order.Items);
        _productRepositoryMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
