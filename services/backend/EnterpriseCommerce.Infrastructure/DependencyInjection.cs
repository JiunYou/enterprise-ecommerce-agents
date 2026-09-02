using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Application.Inventory;
using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EnterpriseCommerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Database") 
            ?? throw new InvalidOperationException("ConnectionStrings:Database is missing.");

        services.AddDbContext<EnterpriseCommerceDbContext>((sp, options) => {
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 21)));
            options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationUnitOfWork>(sp => sp.GetRequiredService<EnterpriseCommerceDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<EnterpriseCommerce.Application.Payments.IPaymentAttemptRepository, PaymentAttemptRepository>();
        services.AddScoped<EnterpriseCommerce.Application.Payments.IPaymentWebhookReceiptRepository, PaymentWebhookReceiptRepository>();
        // In production, no real provider is configured yet. 
        // We register a stub that throws to ensure it fails closed until a real provider is implemented.
        services.AddScoped<EnterpriseCommerce.Application.Payments.IPaymentProvider>(sp => throw new NotImplementedException("A real payment provider is not configured."));
        // Messaging components are currently disabled as they rely on unconfigured external infrastructure.
        // The durable Outbox pattern is used for internal Eventual Consistency via in-process DomainEvent dispatching.

        services.AddHostedService<EnterpriseCommerce.Infrastructure.Persistence.Outbox.OutboxBackgroundService>();
        services.AddHostedService<EnterpriseCommerce.Infrastructure.BackgroundJobs.ExpiredOrdersCleanupBackgroundService>();

        return services;
    }
}
