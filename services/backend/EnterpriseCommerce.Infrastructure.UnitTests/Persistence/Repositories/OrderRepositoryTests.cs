using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence.Repositories;

public class OrderRepositoryTests
{
    private readonly EnterpriseCommerceDbContext _dbContext;
    private readonly OrderRepository _repository;

    public OrderRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(options);
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
}
