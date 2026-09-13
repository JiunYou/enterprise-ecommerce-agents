using EnterpriseCommerce.Domain.Catalog;

namespace EnterpriseCommerce.Application.Abstractions;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        bool? onlyActive = true,
        string? searchTerm = null,
        string? sortBy = null,
        string? sortOrder = null,
        string? category = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    void Add(Product product);
    void Update(Product product);
}
