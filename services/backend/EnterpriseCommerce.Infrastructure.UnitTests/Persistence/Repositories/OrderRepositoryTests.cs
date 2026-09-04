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
}
