using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class PaymentRefundRepository : IPaymentRefundRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public PaymentRefundRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentRefund?> GetByPaymentAttemptIdAsync(PaymentAttemptId paymentAttemptId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PaymentRefunds
            .FirstOrDefaultAsync(r => r.Id == paymentAttemptId, cancellationToken);
    }

    public async Task<IReadOnlyList<PaymentRefund>> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        return await (from refund in _dbContext.PaymentRefunds
                      join attempt in _dbContext.PaymentAttempts on refund.Id equals attempt.Id
                      where attempt.OrderId == orderId
                      select refund).ToListAsync(cancellationToken);
    }

    public async Task<bool> TryCreateIntentAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.PaymentRefunds.Add(refund);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            _dbContext.Entry(refund).State = EntityState.Detached;
            return false;
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var current = (Exception?)ex;
        while (current is not null)
        {
            if (current is MySqlException mySqlException && mySqlException.Number == 1062)
            {
                return true;
            }
            current = current.InnerException;
        }

        return false;
    }
}
