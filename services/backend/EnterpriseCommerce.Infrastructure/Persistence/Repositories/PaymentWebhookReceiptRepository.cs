using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class PaymentWebhookReceiptRepository : IPaymentWebhookReceiptRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public PaymentWebhookReceiptRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(PaymentWebhookReceipt receipt)
    {
        _dbContext.PaymentWebhookReceipts.Add(receipt);
    }

    public async Task<bool> ExistsAsync(string provider, string providerEventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentWebhookReceipts
            .AnyAsync(r => r.Provider == provider && r.ProviderEventId == providerEventId, cancellationToken);
    }
}
