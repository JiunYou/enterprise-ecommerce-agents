using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Common.Models;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Catalog;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Catalog;

[Collection("IntegrationTests")]
public class ProductCategoryMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public ProductCategoryMySqlAcceptanceTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        _dbContextOptions = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.DefaultScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.DefaultScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, options => { });
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    private EnterpriseCommerceDbContext CreateFreshDbContext() => new(_dbContextOptions);

    private HttpClient CreateAdminClient()
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient CreateAnonymousClient() => _factory!.CreateClient();

    [Fact]
    public async Task ProductCategory_RealMySql_EndToEndLifecycle_SatisfiesAllSection40Requirements()
    {
        // 1. Product starts Category empty.
        var runId = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"Product {runId}";
        var sku = $"SKU-CAT-{runId}";
        var price = 299.00m;
        var currency = "TWD";
        var description = $"Description {runId}";
        var imageUrl = "https://example.com/item.jpg";

        var product = Product.Create(originalName, sku, price, currency).Value;
        product.UpdateDescription(description).IsSuccess.Should().BeTrue();
        product.UpdateImageUrl(imageUrl).IsSuccess.Should().BeTrue();
        product.Category.Should().Be(string.Empty);

        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();
        var anonClient = CreateAnonymousClient();

        // 2. Admin updates Category.
        var targetCategory = "  Electronics  ";
        var expectedNormalizedCategory = "Electronics";
        var updateRequest = new UpdateProductCategoryRequest(targetCategory);
        var updateResponse = await adminClient.PutAsJsonAsync($"/api/v1/products/{product.Id}/category", updateRequest);

        // 3. API 200.
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. fresh DbContext reload.
        await using (var db = CreateFreshDbContext())
        {
            var reloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloaded.Should().NotBeNull();

            // 5. normalized Category persisted.
            reloaded!.Category.Should().Be(expectedNormalizedCategory);

            // 6. Product invariants unchanged.
            reloaded.Id.Should().Be(product.Id);
            reloaded.Name.Should().Be(originalName);
            reloaded.Sku.Should().Be(sku);
            reloaded.Price.Should().Be(price);
            reloaded.Currency.Should().Be(currency);
            reloaded.IsActive.Should().BeTrue();
            reloaded.Description.Should().Be(description);
            reloaded.ImageUrl.Should().Be(imageUrl);
        }

        // 7. public Product list exposes Category.
        var listResponse = await anonClient.GetAsync($"/api/v1/products?searchTerm={sku}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedList = await listResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        pagedList.Should().NotBeNull();
        pagedList!.Items.Should().ContainSingle(p => p.Id == product.Id && p.Category == expectedNormalizedCategory);

        // 8. public Product detail exposes Category.
        var publicDetailResponse = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        publicDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicDetail = await publicDetailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        publicDetail.Should().NotBeNull();
        publicDetail!.Id.Should().Be(product.Id);
        publicDetail.Category.Should().Be(expectedNormalizedCategory);

        // 9. Admin list/detail expose Category.
        var adminListResponse = await adminClient.GetAsync($"/api/v1/admin/products?searchTerm={sku}");
        adminListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminPagedList = await adminListResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        adminPagedList.Should().NotBeNull();
        adminPagedList!.Items.Should().ContainSingle(p => p.Id == product.Id && p.Category == expectedNormalizedCategory);

        var adminDetailResponse = await adminClient.GetAsync($"/api/v1/admin/products/{product.Id}");
        adminDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminDetail = await adminDetailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        adminDetail.Should().NotBeNull();
        adminDetail!.Id.Should().Be(product.Id);
        adminDetail.Category.Should().Be(expectedNormalizedCategory);

        // 10. inactive Product may update Category.
        var deactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateInactiveResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/category",
            new UpdateProductCategoryRequest("Gadgets"));
        updateInactiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = CreateFreshDbContext())
        {
            var inactiveReloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            inactiveReloaded.Should().NotBeNull();
            inactiveReloaded!.Category.Should().Be("Gadgets");
            inactiveReloaded.IsActive.Should().BeFalse();
        }

        // 11. Category clear persists empty.
        var clearResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/category",
            new UpdateProductCategoryRequest("   "));
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = CreateFreshDbContext())
        {
            var clearedReloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            clearedReloaded.Should().NotBeNull();
            clearedReloaded!.Category.Should().Be(string.Empty);
            clearedReloaded.IsActive.Should().BeFalse();
        }

        // 12-17: 建立專屬批次商品以驗證分類篩選、組合與探索端點
        var prefix = $"TEST-{runId}";
        var p1 = Product.Create($"{prefix} Novel", $"SKU-{prefix}-1", 100m, "TWD").Value;
        p1.UpdateCategory("Books");

        var p2 = Product.Create($"{prefix} Science", $"SKU-{prefix}-2", 200m, "TWD").Value;
        p2.UpdateCategory("Books");

        var p3 = Product.Create($"{prefix} Smartphone", $"SKU-{prefix}-3", 500m, "TWD").Value;
        p3.UpdateCategory("Electronics");

        var p4 = Product.Create($"{prefix} Uncategorized", $"SKU-{prefix}-4", 50m, "TWD").Value;
        // 未設定 category (預設 "")

        var p5 = Product.Create($"{prefix} Inactive Toy", $"SKU-{prefix}-5", 80m, "TWD").Value;
        p5.UpdateCategory("Toys");
        p5.Deactivate();

        await using (var db = CreateFreshDbContext())
        {
            db.Products.AddRange(p1, p2, p3, p4, p5);
            await db.SaveChangesAsync();
        }

        // 12. GET /api/v1/products?category=... filters correctly.
        var filterBooksResponse = await anonClient.GetAsync($"/api/v1/products?category=Books&searchTerm={prefix}");
        filterBooksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var booksPaged = await filterBooksResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        booksPaged.Should().NotBeNull();
        booksPaged!.Items.Should().HaveCount(2);
        booksPaged.Items.Should().OnlyContain(x => x.Category == "Books");

        // 13. filtered totalCount correct.
        booksPaged.TotalCount.Should().Be(2);

        // 14. category + search compose.
        var searchComposeResponse = await anonClient.GetAsync($"/api/v1/products?category=Books&searchTerm={prefix}+Novel");
        searchComposeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchComposePaged = await searchComposeResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        searchComposePaged.Should().NotBeNull();
        searchComposePaged!.Items.Should().ContainSingle();
        searchComposePaged.Items[0].Sku.Should().Be($"SKU-{prefix}-1");
        searchComposePaged.TotalCount.Should().Be(1);

        // 15. category + sort/pagination compose.
        var sortPaginationResponse = await anonClient.GetAsync(
            $"/api/v1/products?category=Books&searchTerm={prefix}&sortBy=Price&sortOrder=desc&page=1&pageSize=1");
        sortPaginationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sortPaged = await sortPaginationResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        sortPaged.Should().NotBeNull();
        sortPaged!.Items.Should().ContainSingle();
        sortPaged.Items[0].Sku.Should().Be($"SKU-{prefix}-2"); // 200m > 100m
        sortPaged.TotalCount.Should().Be(2);
        sortPaged.HasNextPage.Should().BeTrue();

        // 16. GET /api/v1/products/categories returns only active non-empty distinct sorted categories.
        var categoriesResponse = await anonClient.GetAsync("/api/v1/products/categories");
        categoriesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeCategories = await categoriesResponse.Content.ReadFromJsonAsync<List<string>>();
        activeCategories.Should().NotBeNull();
        activeCategories.Should().Contain("Books");
        activeCategories.Should().Contain("Electronics");
        activeCategories.Should().NotContain(string.Empty);
        // 驗證排序 (由小到大)
        var expectedOrder = activeCategories!.OrderBy(c => c, StringComparer.Ordinal).ToList();
        activeCategories.Should().Equal(expectedOrder);

        // 17. inactive-only Category absent from public category discovery.
        activeCategories.Should().NotContain("Toys");
    }
}
