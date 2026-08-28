using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class PaymentAttemptRepository : IPaymentAttemptRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public PaymentAttemptRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(PaymentAttempt paymentAttempt)
    {
        _dbContext.PaymentAttempts.Add(paymentAttempt);
    }

    public async Task<PaymentAttempt?> GetByIdAsync(PaymentAttemptId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PaymentAttempt?> GetActivePendingAttemptAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentAttempts
            .Where(p => p.OrderId == orderId && p.Status == PaymentAttemptStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PaymentAttempt?> GetByProviderTransactionIdAsync(string provider, string providerTransactionId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Provider == provider && p.ProviderTransactionId == providerTransactionId, cancellationToken);
    }
}
