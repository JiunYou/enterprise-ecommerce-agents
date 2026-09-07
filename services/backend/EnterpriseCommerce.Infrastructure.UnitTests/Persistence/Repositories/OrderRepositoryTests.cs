using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence.Repositories;

public class OrderRepositoryTests
{
    private readonly DbContextOptions<EnterpriseCommerceDbContext> _options;
    private readonly EnterpriseCommerceDbContext _dbContext;
    private readonly OrderRepository _repository;

    public OrderRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(_options);
        _repository = new OrderRepository(_dbContext);
    }

    [Fact]
    public async Task Add_ShouldAddOrderToContext()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "USD"), 2);

        // Act
        _repository.Add(order);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedOrder = await _dbContext.Orders.Include(o => o.Items).FirstOrDefaultAsync();
        savedOrder.Should().NotBeNull();
        savedOrder!.Id.Should().Be(order.Id);
        savedOrder.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderExists_ShouldReturnOrderWithItems()
    {
        // Arrange
        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(50, "USD"), 3);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.Items.Should().HaveCount(1);
        result.TotalAmount.Amount.Should().Be(150);
    }

    [Fact]
    public async Task GetByIdAsync_WhenOrderDoesNotExist_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(new OrderId(Guid.NewGuid()));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingOrderByCustomerIdAsync_WhenPendingOrderExists_ShouldReturnOrderWithItems()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(200, "TWD"), 2);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetPendingOrderByCustomerIdAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(customerId);
        result.Status.Should().Be(OrderStatus.Pending);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPendingOrderByCustomerIdAsync_WhenOrderNotPending_ShouldReturnNull()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(200, "TWD"), 2);
        var shippingAddress = ShippingAddress.Create("Test Recipient", "0912345678", "TW", "100", "Taipei", "123 St").Value;
        order.Submit(shippingAddress, DateTimeOffset.UtcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetPendingOrderByCustomerIdAsync(customerId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnOrderWithShippingAddress_WhenSubmittedWithShippingAddress()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "TWD");
        order.AddItem(new ProductId(Guid.NewGuid()), new Money(200, "TWD"), 1);
        var shippingAddress = ShippingAddress.Create("Jane Doe", "0912345678", "TW", "100", "Taipei", "123 Main St", "Suite 2").Value;
        order.Submit(shippingAddress, DateTimeOffset.UtcNow);
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.ShippingAddress.Should().NotBeNull();
        result.ShippingAddress!.RecipientName.Should().Be("Jane Doe");
        result.ShippingAddress.Phone.Should().Be("0912345678");
        result.ShippingAddress.CountryCode.Should().Be("TW");
        result.ShippingAddress.PostalCode.Should().Be("100");
        result.ShippingAddress.City.Should().Be("Taipei");
        result.ShippingAddress.AddressLine1.Should().Be("123 Main St");
        result.ShippingAddress.AddressLine2.Should().Be("Suite 2");
    }

    [Fact]
    public async Task GetPendingOrderByCustomerIdAsync_ShouldNotCreateAnyRecordInDatabase_WhenNoneExists()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var countBefore = await _dbContext.Orders.CountAsync();
        countBefore.Should().Be(0);

        // Act
        var result = await _repository.GetPendingOrderByCustomerIdAsync(customerId);

        // Assert
        result.Should().BeNull();
        var countAfter = await _dbContext.Orders.CountAsync();
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_ShouldEnforceCustomerIdIsolationAndMembershipRule()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "Addr").Value;

        // 1. Customer A: 正常已送出訂單 (Submitted)
        var orderA1 = Order.Create(customerA, "TWD");
        orderA1.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        orderA1.Submit(address, DateTimeOffset.UtcNow.AddHours(-3));
        _dbContext.Orders.Add(orderA1);

        // 2. Customer A: 已送出且後續取消之訂單 (Submitted + Cancelled) -> 必須包含！
        var orderA2 = Order.Create(customerA, "TWD");
        orderA2.AddItem(new ProductId(Guid.NewGuid()), new Money(200, "TWD"), 1);
        orderA2.Submit(address, DateTimeOffset.UtcNow.AddHours(-2));
        orderA2.Cancel();
        _dbContext.Orders.Add(orderA2);

        // 3. Customer A: 購物車/未送出訂單 (Pending, SubmittedAt == null) -> 必須排除！
        var orderA3Pending = Order.Create(customerA, "TWD");
        orderA3Pending.AddItem(new ProductId(Guid.NewGuid()), new Money(300, "TWD"), 1);
        _dbContext.Orders.Add(orderA3Pending);

        // 4. Customer A: 未送出即取消的購物車 (Cancelled, SubmittedAt == null) -> 必須排除！
        var orderA4CancelledUnsubmitted = Order.Create(customerA, "TWD");
        orderA4CancelledUnsubmitted.Cancel();
        _dbContext.Orders.Add(orderA4CancelledUnsubmitted);

        // 5. Customer B: 已送出訂單 (Submitted, Customer B) -> 必須被 Customer A 隔離排除！
        var orderB = Order.Create(customerB, "TWD");
        orderB.AddItem(new ProductId(Guid.NewGuid()), new Money(400, "TWD"), 1);
        orderB.Submit(address, DateTimeOffset.UtcNow.AddHours(-1));
        _dbContext.Orders.Add(orderB);

        await _dbContext.SaveChangesAsync();

        // Act
        var resultA = await _repository.GetCustomerOrderHistoryAsync(customerA);
        var resultB = await _repository.GetCustomerOrderHistoryAsync(customerB);

        // Assert
        // Customer A: 只有 orderA2 (最新) 與 orderA1
        resultA.Should().HaveCount(2);
        resultA[0].Id.Should().Be(orderA2.Id);
        resultA[0].Status.Should().Be(OrderStatus.Cancelled);
        resultA[0].SubmittedAt.Should().NotBeNull();
        resultA[1].Id.Should().Be(orderA1.Id);
        resultA[1].Status.Should().Be(OrderStatus.Submitted);

        // 驗證未送出的 Pending 與未送出的 Cancelled 均不存在
        resultA.Select(o => o.Id).Should().NotContain(orderA3Pending.Id);
        resultA.Select(o => o.Id).Should().NotContain(orderA4CancelledUnsubmitted.Id);

        // 驗證 Customer B 訂單絕不出現在 Customer A
        resultA.Select(o => o.Id).Should().NotContain(orderB.Id);

        // Customer B: 只有 orderB
        resultB.Should().HaveCount(1);
        resultB[0].Id.Should().Be(orderB.Id);
    }
}
