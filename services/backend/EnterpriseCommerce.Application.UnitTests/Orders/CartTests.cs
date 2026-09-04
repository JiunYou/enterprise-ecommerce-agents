using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.AddItemToCart;
using EnterpriseCommerce.Application.Orders.Commands.RemoveCartItem;
using EnterpriseCommerce.Application.Orders.Commands.UpdateCartItemQuantity;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders;

public class CartTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task GetCart_WhenNoPendingOrder_ShouldReturnEmptyCart()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new GetCartQueryHandler(_orderRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetCartQuery(customerId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Id);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0m, result.Value.TotalAmount);
    }

    [Fact]
    public async Task GetCart_WhenPendingOrderExists_ShouldReturnProjectedCart()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(100m, "USD"), 2);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new GetCartQueryHandler(_orderRepositoryMock.Object);

        // Act
        var result = await handler.Handle(new GetCartQuery(customerId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(order.Id.Value, result.Value.Id);
        Assert.Single(result.Value.Items);
        Assert.Equal(200m, result.Value.TotalAmount);
        Assert.Equal(2, result.Value.Items.First().Quantity);
    }

    [Fact]
    public async Task AddItemToCart_WhenNoPendingOrder_ShouldCreateNewPendingOrderWithProductCurrency()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Book", "SKU-B", 250m, "TWD").Value;

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        Order? createdOrder = null;
        _orderRepositoryMock.Setup(r => r.Add(It.IsAny<Order>()))
            .Callback<Order>(o => createdOrder = o);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new AddItemToCartCommand(customerId, product.Id, 2),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(createdOrder);
        Assert.Equal("TWD", createdOrder!.Currency);
        Assert.Equal(OrderStatus.Pending, createdOrder.Status);
        Assert.Equal(customerId, createdOrder.CustomerId);
        Assert.Single(createdOrder.Items);
        Assert.Equal(500m, createdOrder.TotalAmount.Amount);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemToCart_WhenPendingOrderExists_ShouldReuseExistingPendingOrder()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var existingOrder = Order.Create(customerId, "TWD");
        var product = Product.Create("Pen", "SKU-P", 30m, "TWD").Value;

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new AddItemToCartCommand(customerId, product.Id, 4),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        _orderRepositoryMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        Assert.Single(existingOrder.Items);
        Assert.Equal(120m, existingOrder.TotalAmount.Amount);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemToCart_WhenProductInactive_ShouldReturnError()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Legacy", "SKU-L", 100m, "USD").Value;
        product.Deactivate();

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new AddItemToCartCommand(customerId, product.Id, 1),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.NotActive, result.Error);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenItemExists_ShouldUpdateAndSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50m, "USD"), 1);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new UpdateCartItemQuantityCommand(customerId, productId.Value, 5),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, order.Items.First().Quantity);
        Assert.Equal(250m, order.TotalAmount.Amount);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveCartItem_WhenItemExists_ShouldRemoveAndSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50m, "USD"), 1);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new RemoveCartItemCommandHandler(
            _orderRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new RemoveCartItemCommand(customerId, productId.Value),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(order.Items);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SequentialCartOperations_ReusesSinglePendingOrder()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        Order? storedOrder = null;
        var createdOrders = new List<Order>();

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedOrder);

        _orderRepositoryMock.Setup(r => r.Add(It.IsAny<Order>()))
            .Callback<Order>(o =>
            {
                storedOrder = o;
                createdOrders.Add(o);
            });

        var product = Product.Create("Item 1", "SKU-1", 100m, "USD").Value;
        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act 1: 首次加購
        var res1 = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 1), CancellationToken.None);

        // Act 2: 再次加購（同顧客）
        var res2 = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 2), CancellationToken.None);

        // Assert
        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);
        Assert.Single(createdOrders); // 僅被建立過一次
        Assert.Equal(storedOrder!.Id.Value, res1.Value.Id);
        Assert.Equal(storedOrder.Id.Value, res2.Value.Id);
        Assert.Single(storedOrder.Items);
        Assert.Equal(3, storedOrder.Items.First().Quantity);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
