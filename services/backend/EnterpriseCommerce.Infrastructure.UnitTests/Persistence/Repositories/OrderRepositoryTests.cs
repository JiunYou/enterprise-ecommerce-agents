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
        var (resultA, _) = await _repository.GetCustomerOrderHistoryAsync(customerA, page: 1, pageSize: 25);
        var (resultB, _) = await _repository.GetCustomerOrderHistoryAsync(customerB, page: 1, pageSize: 25);

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

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_WithPagination_ShouldSlicePagesCorrectlyAndReturnTotalCount()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "Addr").Value;
        var baseTime = DateTimeOffset.UtcNow;

        var order1 = Order.Create(customerId, "TWD");
        order1.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        order1.Submit(address, baseTime.AddHours(-3)); // 最舊

        var order2 = Order.Create(customerId, "TWD");
        order2.AddItem(new ProductId(Guid.NewGuid()), new Money(200, "TWD"), 1);
        order2.Submit(address, baseTime.AddHours(-2)); // 次新

        var order3 = Order.Create(customerId, "TWD");
        order3.AddItem(new ProductId(Guid.NewGuid()), new Money(300, "TWD"), 1);
        order3.Submit(address, baseTime.AddHours(-1)); // 最新

        _dbContext.Orders.AddRange(order1, order2, order3);
        await _dbContext.SaveChangesAsync();

        // Act - Page 1, Size 2
        var (page1Items, totalCount1) = await _repository.GetCustomerOrderHistoryAsync(customerId, page: 1, pageSize: 2);
        // Act - Page 2, Size 2
        var (page2Items, totalCount2) = await _repository.GetCustomerOrderHistoryAsync(customerId, page: 2, pageSize: 2);

        // Assert
        totalCount1.Should().Be(3);
        totalCount2.Should().Be(3);

        page1Items.Should().HaveCount(2);
        page1Items[0].Id.Should().Be(order3.Id);
        page1Items[1].Id.Should().Be(order2.Id);

        page2Items.Should().HaveCount(1);
        page2Items[0].Id.Should().Be(order1.Id);
    }

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_WithPagination_ShouldEnforceCustomerIsolationAndMembershipRules()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "Addr").Value;

        // 1. Customer A: 已送出訂單
        var orderA1 = Order.Create(customerA, "TWD");
        orderA1.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        orderA1.Submit(address, DateTimeOffset.UtcNow.AddHours(-3));

        // 2. Customer A: 已送出且已取消訂單 (必須包含)
        var orderA2 = Order.Create(customerA, "TWD");
        orderA2.AddItem(new ProductId(Guid.NewGuid()), new Money(200, "TWD"), 1);
        orderA2.Submit(address, DateTimeOffset.UtcNow.AddHours(-2));
        orderA2.Cancel();

        // 3. Customer A: 未送出之購物車 (必須排除)
        var orderA3Pending = Order.Create(customerA, "TWD");
        orderA3Pending.AddItem(new ProductId(Guid.NewGuid()), new Money(300, "TWD"), 1);

        // 4. Customer A: 未送出即取消之購物車 (必須排除)
        var orderA4CancelledUnsubmitted = Order.Create(customerA, "TWD");
        orderA4CancelledUnsubmitted.Cancel();

        // 5. Customer B: 已送出訂單 (必須隔離排除)
        var orderB = Order.Create(customerB, "TWD");
        orderB.AddItem(new ProductId(Guid.NewGuid()), new Money(400, "TWD"), 1);
        orderB.Submit(address, DateTimeOffset.UtcNow.AddHours(-1));

        _dbContext.Orders.AddRange(orderA1, orderA2, orderA3Pending, orderA4CancelledUnsubmitted, orderB);
        await _dbContext.SaveChangesAsync();

        // Act
        var (itemsA, totalCountA) = await _repository.GetCustomerOrderHistoryAsync(customerA, page: 1, pageSize: 10);
        var (itemsB, totalCountB) = await _repository.GetCustomerOrderHistoryAsync(customerB, page: 1, pageSize: 10);

        // Assert - Customer A
        totalCountA.Should().Be(2);
        itemsA.Should().HaveCount(2);
        itemsA[0].Id.Should().Be(orderA2.Id);
        itemsA[1].Id.Should().Be(orderA1.Id);
        itemsA.Select(o => o.Id).Should().NotContain(orderA3Pending.Id);
        itemsA.Select(o => o.Id).Should().NotContain(orderA4CancelledUnsubmitted.Id);
        itemsA.Select(o => o.Id).Should().NotContain(orderB.Id);

        // Assert - Customer B
        totalCountB.Should().Be(1);
        itemsB.Should().HaveCount(1);
        itemsB[0].Id.Should().Be(orderB.Id);
    }

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_WithPagination_ShouldEnforceDeterministicOrdering_WhenSubmittedAtEqual()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var address = ShippingAddress.Create("Test", "0912345678", "TW", "100", "Taipei", "Addr").Value;
        var sameSubmittedAt = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

        var order1 = Order.Create(customerId, "TWD");
        order1.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        order1.Submit(address, sameSubmittedAt);

        var order2 = Order.Create(customerId, "TWD");
        order2.AddItem(new ProductId(Guid.NewGuid()), new Money(100, "TWD"), 1);
        order2.Submit(address, sameSubmittedAt);

        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        // 預期較大 OrderId 的排在前面 (Id DESC)
        var expectedFirst = order1.Id.Value.CompareTo(order2.Id.Value) > 0 ? order1 : order2;
        var expectedSecond = order1.Id.Value.CompareTo(order2.Id.Value) > 0 ? order2 : order1;

        // Act
        var (items, totalCount) = await _repository.GetCustomerOrderHistoryAsync(customerId, page: 1, pageSize: 10);

        // Assert
        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
        items[0].Id.Should().Be(expectedFirst.Id);
        items[1].Id.Should().Be(expectedSecond.Id);
    }
}
