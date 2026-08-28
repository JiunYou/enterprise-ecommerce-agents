using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence;

public class EnterpriseCommerceDbContextTests
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public EnterpriseCommerceDbContextTests()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(options);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSaveOutboxMessage_WhenEntityHasDomainEvents()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        
        _dbContext.Orders.Add(order);

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        var outboxMessages = await _dbContext.OutboxMessages.ToListAsync();
        outboxMessages.Should().HaveCount(1);
        
        var message = outboxMessages.Single();
        message.EventType.Should().Be("OrderCreatedDomainEvent");
        message.OccurredOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Ensure Domain Events are cleared
        var getMethod = order.GetType().GetMethod("GetDomainEvents");
        var events = (IReadOnlyCollection<IDomainEvent>)getMethod!.Invoke(order, null)!;
        events.Should().BeEmpty();
    }
}
