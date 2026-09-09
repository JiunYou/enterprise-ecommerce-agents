using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Orders.Commands.AddItemToCart;
using EnterpriseCommerce.Application.Orders.Commands.RemoveCartItem;
using EnterpriseCommerce.Application.Orders.Commands.UpdateCartItemQuantity;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Application.UnitTests.Orders;

public class CartTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IProductRepository> _productRepositoryMock = new();
    private readonly Mock<IInventoryRepository> _inventoryRepositoryMock = new();
    private readonly Mock<IApplicationUnitOfWork> _unitOfWorkMock = new();

    private static InventoryItem CreateInventory(Guid productId, int availableQuantity)
    {
        var inventory = InventoryItem.Create(new ProductReference(productId));
        if (availableQuantity > 0)
        {
            inventory.IncreaseStock(new StockQuantity(availableQuantity));
        }
        return inventory;
    }

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

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, 10));

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        Order? createdOrder = null;
        _orderRepositoryMock.Setup(r => r.Add(It.IsAny<Order>()))
            .Callback<Order>(o => createdOrder = o);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
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

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, 10));

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
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
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(
            new AddItemToCartCommand(customerId, product.Id, 1),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(ProductErrors.NotActive, result.Error);
        _inventoryRepositoryMock.Verify(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == productId.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(productId.Value, 10));

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
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

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.Is<ProductReference>(p => p.Value == product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, 10));

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
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

    // =========================================================================
    // SECTION 14: APPLICATION BEHAVIORAL PROTECTION TESTS
    // =========================================================================

    [Fact]
    public async Task AddItemToCart_WhenInventoryNotFound_ShouldReturnInsufficientStock_AndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Product", "SKU-1", 100m, "USD").Value;

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 1), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        _orderRepositoryMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemToCart_WhenRequestedQuantityExceedsAvailable_ShouldReturnInsufficientStock_AndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Product", "SKU-1", 100m, "USD").Value;

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, 2));

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 3), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        _orderRepositoryMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemToCart_WhenCumulativeQuantityExceedsAvailable_ShouldReturnInsufficientStock_AndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Product", "SKU-1", 100m, "USD").Value;

        var existingOrder = Order.Create(customerId, "USD");
        existingOrder.AddItem(new ProductId(product.Id), new Money(100m, "USD"), 2);

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, 3)); // Available: 3, Existing: 2, Requested: 2 -> Desired: 4 > 3

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 2), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(2, existingOrder.Items.First().Quantity); // Unchanged
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemToCart_WhenCumulativeQuantityExceedsInt32Range_ShouldReturnInsufficientStock()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Product", "SKU-1", 100m, "USD").Value;

        var existingOrder = Order.Create(customerId, "USD");
        existingOrder.AddItem(new ProductId(product.Id), new Money(100m, "USD"), int.MaxValue);

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, int.MaxValue));

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 1), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(int.MaxValue, existingOrder.Items.First().Quantity);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemToCart_WhenSufficientStock_ShouldSucceed_AndSaveOnce()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Product", "SKU-1", 100m, "USD").Value;

        var existingOrder = Order.Create(customerId, "USD");
        existingOrder.AddItem(new ProductId(product.Id), new Money(100m, "USD"), 1);

        _productRepositoryMock.Setup(r => r.GetByIdAsync(new ProductId(product.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingOrder);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(product.Id, 5)); // Available: 5, Existing: 1, Requested: 2 -> Desired: 3 <= 5

        var handler = new AddItemToCartCommandHandler(
            _orderRepositoryMock.Object,
            _productRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new AddItemToCartCommand(customerId, product.Id, 2), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, existingOrder.Items.First().Quantity);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenNoPendingOrder_ShouldReturnItemNotFound_AndNotQueryInventory()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Order?)null);

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new UpdateCartItemQuantityCommand(customerId, productId, 5), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ItemNotFound, result.Error);
        _inventoryRepositoryMock.Verify(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenItemNotInOrder_ShouldReturnItemNotFound_AndNotQueryInventory()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD"); // Empty order
        var productId = Guid.NewGuid();

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new UpdateCartItemQuantityCommand(customerId, productId, 5), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.ItemNotFound, result.Error);
        _inventoryRepositoryMock.Verify(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenInventoryNotFound_ShouldReturnInsufficientStock_AndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50m, "USD"), 2);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryItem?)null);

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new UpdateCartItemQuantityCommand(customerId, productId.Value, 5), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(2, order.Items.First().Quantity); // Unchanged
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenQuantityExceedsAvailable_ShouldReturnInsufficientStock_AndNotSave()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50m, "USD"), 2);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(productId.Value, 3)); // Available: 3, Requested: 4 > 3

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new UpdateCartItemQuantityCommand(customerId, productId.Value, 4), CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(InventoryErrors.InsufficientStock, result.Error);
        Assert.Equal(2, order.Items.First().Quantity); // Unchanged
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WhenSufficientStock_ShouldSucceed_AndSaveOnce()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        var productId = new ProductId(Guid.NewGuid());
        order.AddItem(productId, new Money(50m, "USD"), 2);

        _orderRepositoryMock.Setup(r => r.GetPendingOrderByCustomerIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        _inventoryRepositoryMock.Setup(r => r.GetByProductIdAsync(It.IsAny<ProductReference>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInventory(productId.Value, 5)); // Available: 5, Requested: 4 <= 5

        var handler = new UpdateCartItemQuantityCommandHandler(
            _orderRepositoryMock.Object,
            _inventoryRepositoryMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new UpdateCartItemQuantityCommand(customerId, productId.Value, 4), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(4, order.Items.First().Quantity);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
