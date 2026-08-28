using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseCommerce.Application.UnitTests.Events;

public class DomainEventDispatcherTests
{
    private record TestDomainEvent(Guid EventId) : DomainEvent(EventId, DateTime.UtcNow);
    
    private class TestDomainEventHandler : IDomainEventHandler<TestDomainEvent>
    {
        public bool IsHandled { get; private set; }

        public Task HandleAsync(TestDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            IsHandled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DispatchAsync_ShouldCallRegisteredHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        var handler = new TestDomainEventHandler();
        services.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler);
        var serviceProvider = services.BuildServiceProvider();
        
        var dispatcher = new DomainEventDispatcher(serviceProvider);
        var testEvent = new TestDomainEvent(Guid.NewGuid());

        // Act
        await dispatcher.DispatchAsync(testEvent);

        // Assert
        Assert.True(handler.IsHandled);
    }

    [Fact]
    public async Task DispatchAsync_ShouldNotThrow_WhenNoHandlerRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider);
        var testEvent = new TestDomainEvent(Guid.NewGuid());

        // Act
        var exception = await Record.ExceptionAsync(() => dispatcher.DispatchAsync(testEvent));

        // Assert
        Assert.Null(exception);
    }
}
