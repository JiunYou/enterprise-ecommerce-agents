using EnterpriseCommerce.Application.Abstractions;
using EnterpriseCommerce.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly EnterpriseCommerceDbContext _dbContext;

    public ProductRepository(EnterpriseCommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        bool? onlyActive = true,
        string? searchTerm = null,
        string? sortBy = null,
        string? sortOrder = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Product> query = _dbContext.Products.AsNoTracking();

        if (onlyActive.HasValue)
        {
            query = query.Where(p => p.IsActive == onlyActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var trimmedSearch = searchTerm.Trim();
            query = query.Where(p => p.Name.Contains(trimmedSearch) || p.Sku.Contains(trimmedSearch));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        bool isDescending = string.Equals(sortOrder?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        string normalizedSortBy = sortBy?.Trim().ToLowerInvariant() ?? "name";

        query = normalizedSortBy switch
        {
            "price" => isDescending ? query.OrderByDescending(p => p.Price) : query.OrderBy(p => p.Price),
            _ => isDescending ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public void Add(Product product)
    {
        _dbContext.Products.Add(product);
    }

    public void Update(Product product)
    {
        _dbContext.Products.Update(product);
    }
}
