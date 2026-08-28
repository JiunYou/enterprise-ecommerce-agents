using Microsoft.AspNetCore.TestHost;
using EnterpriseCommerce.Application.Payments;

using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;

using System.Diagnostics;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Application.Payments.Commands.InitiatePayment;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Payments;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.BackgroundJobs;

[Collection("IntegrationTests")]
public class PaymentConcurrencyTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public PaymentConcurrencyTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using (var dbContext = new EnterpriseCommerceDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentProvider, EnterpriseCommerce.WebApi.IntegrationTests.Payments.DummyPaymentProvider>();
            });
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitiatePayment_ConcurrentRequests_OnlyOneSucceeds()
    {
        // 1. Arrange a real database and a Submitted Order
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
        
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var order = Order.Create(Guid.NewGuid(), "USD");
        order.AddItem(new ProductId(Guid.NewGuid()), new EnterpriseCommerce.Domain.Orders.ValueObjects.Money(100m, "USD"), 1);
        order.Submit(DateTimeOffset.UtcNow);
        
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var idempotencyKey = Guid.NewGuid();
        
        // 2. Act: Send 5 concurrent initiation requests
        var tasks = new List<Task<Domain.Primitives.Result<EnterpriseCommerce.Application.Payments.InitiatePaymentResponse>>>();
        
        for (int i = 0; i < 5; i++)
        {
            var uniqueIdempotencyKey = Guid.NewGuid();
            tasks.Add(Task.Run(async () => 
            {
                using var innerScope = _factory.Services.CreateScope();
                var innerDb = innerScope.ServiceProvider.GetRequiredService<EnterpriseCommerceDbContext>();
                var innerSender = innerScope.ServiceProvider.GetRequiredService<ISender>();
                var command = new InitiatePaymentCommand(order.Id.Value, uniqueIdempotencyKey, order.CustomerId);
                return await innerSender.Send(command);
            }));
        }

        var results = await Task.WhenAll(tasks);

        // 3. Assert
        var successes = results.Count(r => r.IsSuccess);
        var failures = results.Count(r => r.IsFailure);
        

        successes.Should().Be(1, "Because pessimistic row lock ensures only one request creates the pending attempt");
        failures.Should().Be(4);
        
        var attemptCount = await db.PaymentAttempts.CountAsync();
        attemptCount.Should().Be(1);
    }
}
