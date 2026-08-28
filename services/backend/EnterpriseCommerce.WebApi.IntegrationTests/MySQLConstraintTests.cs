using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System;
using System.Threading.Tasks;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

[Collection("IntegrationTests")]
public class MySQLConstraintTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private EnterpriseCommerceDbContext _dbContext = default!;

    public MySQLConstraintTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task PaymentAttempt_DuplicateOrderIdAndIdempotencyKey_ThrowsDbUpdateException()
    {
        var orderId = new OrderId(Guid.NewGuid());
        var idempotencyKey = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(orderId, new Money(100m, "USD"), "provider", idempotencyKey, DateTimeOffset.UtcNow);
        var attempt2 = PaymentAttempt.Create(orderId, new Money(100m, "USD"), "provider", idempotencyKey, DateTimeOffset.UtcNow);

        _dbContext.PaymentAttempts.Add(attempt1);
        await _dbContext.SaveChangesAsync();

        _dbContext.PaymentAttempts.Add(attempt2);

        var action = async () => await _dbContext.SaveChangesAsync();

        var ex = await action.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().NotBeNull();
        ex.Which.InnerException!.Message.Should().Contain("Duplicate entry");
    }

    [Fact]
    public async Task PaymentWebhookReceipt_DuplicateProviderAndEventId_ThrowsDbUpdateException()
    {
        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var provider = "dummy_provider";
        var eventId = Guid.NewGuid().ToString();

        var receipt1 = PaymentWebhookReceipt.Create(provider, eventId, attemptId, DateTimeOffset.UtcNow);
        var receipt2 = PaymentWebhookReceipt.Create(provider, eventId, attemptId, DateTimeOffset.UtcNow);

        _dbContext.PaymentWebhookReceipts.Add(receipt1);
        await _dbContext.SaveChangesAsync();

        _dbContext.PaymentWebhookReceipts.Add(receipt2);

        var action = async () => await _dbContext.SaveChangesAsync();

        var ex = await action.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().NotBeNull();
        ex.Which.InnerException!.Message.Should().Contain("Duplicate entry");
    }

    [Fact]
    public async Task PaymentAttempt_DuplicateProviderAndProviderTransactionId_ThrowsDbUpdateException()
    {
        var orderId1 = new OrderId(Guid.NewGuid());
        var orderId2 = new OrderId(Guid.NewGuid());
        
        var attempt1 = PaymentAttempt.Create(orderId1, new Money(100m, "USD"), "dummy", Guid.NewGuid(), DateTimeOffset.UtcNow);
        var attempt2 = PaymentAttempt.Create(orderId2, new Money(100m, "USD"), "dummy", Guid.NewGuid(), DateTimeOffset.UtcNow);

        _dbContext.PaymentAttempts.Add(attempt1);
        _dbContext.PaymentAttempts.Add(attempt2);
        await _dbContext.SaveChangesAsync();

        var txId = "shared_tx_123";
        
        // This simulates a scenario where two different attempts get updated with the SAME provider transaction ID
        attempt1.MarkAsSucceeded(txId, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync();

        attempt2.MarkAsSucceeded(txId, DateTimeOffset.UtcNow);
        var action = async () => await _dbContext.SaveChangesAsync();

        var ex = await action.Should().ThrowAsync<DbUpdateException>();
        ex.Which.InnerException.Should().NotBeNull();
        ex.Which.InnerException!.Message.Should().Contain("Duplicate entry");
    }
}
