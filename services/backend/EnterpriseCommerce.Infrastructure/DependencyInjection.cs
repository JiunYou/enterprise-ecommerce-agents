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
        services.AddScoped<ICustomerIdentityStore, CustomerIdentityStore>();

        services.Configure<EnterpriseCommerce.Infrastructure.Payments.ECPay.ECPayPaymentOptions>(options =>
        {
            var section = configuration.GetSection("Payments:ECPay");
            options.MerchantId = section["MerchantId"];
            options.HashKey = section["HashKey"];
            options.HashIv = section["HashIv"];
            options.ReturnUrl = section["ReturnUrl"];
            options.ClientBackUrlBase = section["ClientBackUrlBase"]
                ?? section["CustomerWebBaseUrl"]
                ?? configuration["Payments:CheckoutReturnBaseUrl"];
            if (!string.IsNullOrWhiteSpace(section["ActionUrl"]))
            {
                options.ActionUrl = section["ActionUrl"]!;
            }
        });
        services.AddScoped<EnterpriseCommerce.Infrastructure.Payments.ECPay.IECPayPaymentNotificationService, EnterpriseCommerce.Infrastructure.Payments.ECPay.ECPayPaymentNotificationService>();

        // ECPay 為目前唯一的作用中 IPaymentProvider
        services.AddScoped<EnterpriseCommerce.Application.Payments.IPaymentProvider, EnterpriseCommerce.Infrastructure.Payments.ECPay.ECPayPaymentProvider>();
        // Messaging components are currently disabled as they rely on unconfigured external infrastructure.
        // The durable Outbox pattern is used for internal Eventual Consistency via in-process DomainEvent dispatching.

        services.AddHostedService<EnterpriseCommerce.Infrastructure.Persistence.Outbox.OutboxBackgroundService>();
        services.AddHostedService<EnterpriseCommerce.Infrastructure.BackgroundJobs.ExpiredOrdersCleanupBackgroundService>();

        return services;
    }
}
