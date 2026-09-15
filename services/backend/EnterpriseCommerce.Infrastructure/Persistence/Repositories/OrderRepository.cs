using EnterpriseCommerce.Application.Orders;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public OrderRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Order order)
    {
        _dbContext.Orders.Add(order);
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetByIdForUpdateAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        // Requires a transaction to be active.
        var sql = $"SELECT * FROM Orders WHERE Id = '{id.Value}' FOR UPDATE";
        
        // EF Core 8+ syntax for raw SQL on DbSet
        return await _dbContext.Orders
            .FromSqlRaw(sql)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Order?> GetPendingOrderByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetFulfillmentQueueAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.Status == OrderStatus.Paid)
            .OrderBy(o => o.SubmittedAt)
            .ThenBy(o => o.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetAdminOrdersAsync(
        OrderStatus? status,
        OrderId? orderId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        if (orderId is not null)
        {
            query = query.Where(o => o.Id == orderId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.SubmittedAt)
            .ThenBy(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetCustomerOrderHistoryAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId && o.SubmittedAt != null);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(o => o.Items)
            .OrderByDescending(o => o.SubmittedAt)
            .ThenByDescending(o => (Guid)o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<AdminOrderOperationsOverviewData> GetAdminOrderOperationsOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        var formalOrdersQuery = _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.SubmittedAt != null);

        // 1 status-count aggregate query
        var statusCounts = await formalOrdersQuery
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var submittedCount = statusCounts.FirstOrDefault(x => x.Status == OrderStatus.Submitted)?.Count ?? 0;
        var paidCount = statusCounts.FirstOrDefault(x => x.Status == OrderStatus.Paid)?.Count ?? 0;
        var shippedCount = statusCounts.FirstOrDefault(x => x.Status == OrderStatus.Shipped)?.Count ?? 0;
        var cancelledCount = statusCounts.FirstOrDefault(x => x.Status == OrderStatus.Cancelled)?.Count ?? 0;
        var totalCount = submittedCount + paidCount + shippedCount + cancelledCount;

        // 1 recent-orders projection query (up to 5 formal orders, ordered by SubmittedAt DESC, Id DESC)
        var recentOrdersRaw = await formalOrdersQuery
            .OrderByDescending(o => o.SubmittedAt)
            .ThenByDescending(o => (Guid)o.Id)
            .Take(5)
            .Select(o => new
            {
                Id = (Guid)o.Id,
                Status = o.Status,
                Currency = o.Currency,
                SubmittedAt = o.SubmittedAt!.Value,
                Subtotal = o.Items.Sum(i => (decimal?)i.UnitPrice.Amount * i.Quantity) ?? 0m,
                Discount = o.AppliedCouponDiscountAmount ?? 0m
            })
            .ToListAsync(cancellationToken);

        var recentOrders = recentOrdersRaw.Select(r => new AdminOrderOverviewRecentOrderData(
            r.Id,
            r.Status.ToString(),
            r.Currency,
            r.Subtotal - r.Discount,
            r.SubmittedAt
        )).ToList();

        return new AdminOrderOperationsOverviewData(
            totalCount,
            submittedCount,
            paidCount,
            shippedCount,
            cancelledCount,
            recentOrders);
    }
}
