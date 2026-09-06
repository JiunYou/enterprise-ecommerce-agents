using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Events;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Persistence.Identity;
using EnterpriseCommerce.Infrastructure.Persistence.Orders;
using EnterpriseCommerce.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnterpriseCommerce.Infrastructure.Persistence;

public sealed class EnterpriseCommerceDbContext : DbContext, IApplicationUnitOfWork
{
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;
    public DbSet<EnterpriseCommerce.Domain.Catalog.Product> Products { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<PaymentAttempt> PaymentAttempts { get; set; } = null!;
    public DbSet<PaymentWebhookReceipt> PaymentWebhookReceipts { get; set; } = null!;
    public DbSet<CustomerIdentity> CustomerIdentities { get; set; } = null!;
    public DbSet<AdminOrderCancellation> AdminOrderCancellations { get; set; } = null!;

    public EnterpriseCommerceDbContext(DbContextOptions<EnterpriseCommerceDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnterpriseCommerceDbContext).Assembly);
        
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var modifiedAggregates = ChangeTracker
            .Entries()
            .Where(e => e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in modifiedAggregates)
        {
            var versionProperty = entity.GetType().GetProperty("Version");
            if (versionProperty != null && versionProperty.PropertyType == typeof(uint))
            {
                var currentVersion = (uint)versionProperty.GetValue(entity)!;
                versionProperty.SetValue(entity, currentVersion + 1);
            }
        }

        var domainEvents = ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .Select(entity => 
            {
                var getMethod = entity.GetType().GetMethod("GetDomainEvents");
                if (getMethod == null) return null;
                
                var clearMethod = entity.GetType().GetMethod("ClearDomainEvents");
                var events = ((IReadOnlyCollection<IDomainEvent>)getMethod.Invoke(entity, null)!).ToList();
                clearMethod?.Invoke(entity, null);
                
                return events;
            })
            .Where(events => events != null)
            .SelectMany(events => events!)
            .ToList();

        var outboxMessages = domainEvents.Select(domainEvent =>
        {
            var eventType = domainEvent.GetType().Name;
            var payload = JsonSerializer.Serialize((object)domainEvent);

            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOn = domainEvent.OccurredOn,
                EventType = eventType,
                Content = payload
            };
        }).ToList();

        if (outboxMessages.Any())
        {
            await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }
}
