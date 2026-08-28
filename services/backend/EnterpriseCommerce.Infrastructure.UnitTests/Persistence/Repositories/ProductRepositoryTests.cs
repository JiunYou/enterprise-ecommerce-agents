using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Persistence.Repositories;

public class ProductRepositoryTests
{
    private readonly EnterpriseCommerceDbContext _dbContext;
    private readonly ProductRepository _repository;

    public ProductRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EnterpriseCommerceDbContext(options);
        _repository = new ProductRepository(_dbContext);
    }

    [Fact]
    public async Task Add_ShouldAddProductToContext()
    {
        // Arrange
        var product = Product.Create("Test Product", "SKU-REPO-1", 100m, "TWD").Value;

        // Act
        _repository.Add(product);
        await _dbContext.SaveChangesAsync();

        // Assert
        var savedProduct = await _dbContext.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
        savedProduct.Should().NotBeNull();
        savedProduct!.Sku.Should().Be("SKU-REPO-1");
    }

    [Fact]
    public async Task GetBySkuAsync_WhenExists_ShouldReturnProduct()
    {
        // Arrange
        var product = Product.Create("SKU Test", "SKU-REPO-2", 200m, "TWD").Value;
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _repository.GetBySkuAsync("SKU-REPO-2");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(product.Id);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnCorrectPaginationAndFilter()
    {
        // Arrange
        var p1 = Product.Create("Apple", "SKU-A", 50m, "TWD").Value;
        var p2 = Product.Create("Banana", "SKU-B", 30m, "TWD").Value;
        var p3 = Product.Create("Cherry", "SKU-C", 80m, "TWD").Value;
        p3.Deactivate();

        _dbContext.Products.AddRange(p1, p2, p3);
        await _dbContext.SaveChangesAsync();

        // Act 1: onlyActive = true, page 1, pageSize 10
        var (activeItems, activeTotal) = await _repository.GetPagedAsync(1, 10, onlyActive: true);

        // Assert 1
        activeTotal.Should().Be(2);
        activeItems.Should().HaveCount(2);
        activeItems.Select(x => x.Name).Should().ContainInOrder("Apple", "Banana");

        // Act 2: onlyActive = null (all), page 1, pageSize 2
        var (allItems, allTotal) = await _repository.GetPagedAsync(1, 2, onlyActive: null);

        // Assert 2
        allTotal.Should().Be(3);
        allItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPagedAsync_WithSearchTermAndSorting_ShouldFilterAndOrderCorrectly()
    {
        // Arrange
        var p1 = Product.Create("Mechanical Keyboard", "SKU-KB-1", 150m, "TWD").Value;
        var p2 = Product.Create("Wireless Mouse", "SKU-MS-1", 50m, "TWD").Value;
        var p3 = Product.Create("Gaming Keyboard", "SKU-KB-2", 250m, "TWD").Value;

        _dbContext.Products.AddRange(p1, p2, p3);
        await _dbContext.SaveChangesAsync();

        // Act: Search "Keyboard", Sort by Price desc
        var (items, total) = await _repository.GetPagedAsync(1, 10, onlyActive: true, searchTerm: "Keyboard", sortBy: "price", sortOrder: "desc");

        // Assert
        total.Should().Be(2);
        items.Should().HaveCount(2);
        items[0].Sku.Should().Be("SKU-KB-2"); // 250m
        items[1].Sku.Should().Be("SKU-KB-1"); // 150m
    }
}
