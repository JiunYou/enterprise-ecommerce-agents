using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EnterpriseCommerce.Infrastructure;

public static class RefundPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddRefundPersistence(this IServiceCollection services)
    {
        services.AddScoped<IPaymentRefundRepository, PaymentRefundRepository>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
