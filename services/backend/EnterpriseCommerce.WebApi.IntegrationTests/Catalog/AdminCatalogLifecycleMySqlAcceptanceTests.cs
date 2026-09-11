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
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Catalog;

[Collection("IntegrationTests")]
public class AdminCatalogLifecycleMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public AdminCatalogLifecycleMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

    [Fact]
    public async Task AdminCatalog_RealMySql_Lifecycle_MeetsAllAcceptanceCriteria()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-LIFECYCLE-{runId}";
        var originalPrice = 100m;
        var updatedPrice = 250m;

        // 1. 在真實 MySQL 中直接寫入一個初始處於 Active 狀態的 Product
        var product = Product.Create($"Lifecycle Product {runId}", sku, originalPrice, "TWD").Value;
        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();

        // 2. 呼叫管理員調整價格端點 PUT /api/v1/products/{id}/price
        var priceUpdateRequest = new UpdateProductPriceRequest(updatedPrice);
        var priceResponse = await adminClient.PutAsJsonAsync($"/api/v1/products/{product.Id}/price", priceUpdateRequest);
        priceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. 開啟獨立 fresh DbContext，驗證價格已正確持久化至資料庫
        await using (var db = CreateFreshDbContext())
        {
            var reloadedProduct = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloadedProduct.Should().NotBeNull();
            reloadedProduct!.Price.Should().Be(updatedPrice);
            reloadedProduct.IsActive.Should().BeTrue();
        }

        // 4. 呼叫管理員商品停用端點 PUT /api/v1/products/{id}/deactivate
        var deactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. 開啟獨立 fresh DbContext，驗證 IsActive 已成功變更為 false 並持久化
        await using (var db = CreateFreshDbContext())
        {
            var reloadedProduct = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloadedProduct.Should().NotBeNull();
            reloadedProduct!.IsActive.Should().BeFalse();
        }

        // 6. 重複呼叫停用端點，驗證回傳 400 BadRequest (ProductErrors.AlreadyDeactivated)
        var repeatedDeactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/deactivate", null);
        repeatedDeactivateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 7. 管理員查詢商品清單帶入 onlyActive=false，驗證能返回包含該非活躍商品
        var listResponse = await adminClient.GetAsync($"/api/v1/admin/products?onlyActive=false&searchTerm={sku}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listResult = await listResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        listResult.Should().NotBeNull();
        listResult!.Items.Should().ContainSingle(p => p.Id == product.Id && p.IsActive == false);

        // 8. 管理員查詢商品詳情，驗證能正確取得已停用商品資料
        var detailResponse = await adminClient.GetAsync($"/api/v1/admin/products/{product.Id}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailResult = await detailResponse.Content.ReadFromJsonAsync<ProductResponse>();
        detailResult.Should().NotBeNull();
        detailResult!.Id.Should().Be(product.Id);
        detailResult.IsActive.Should().BeFalse();
        detailResult.Price.Should().Be(updatedPrice);
        detailResult.Sku.Should().Be(sku);
    }

    [Fact]
    public async Task AdminCatalog_RealMySql_ReactivateLifecycle_RestoresPublicCatalogEligibility()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-REACTIVATE-{runId}";
        var originalPrice = 120m;

        // 1. 在真實 MySQL 中建立並持久化商品
        var product = Product.Create($"Reactivation Product {runId}", sku, originalPrice, "TWD").Value;
        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();
        var anonClient = _factory!.CreateClient();

        // 2. 先將商品停用（Deactivate）
        var deactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. 驗證公開匿名端點此時查不到該已停用商品 (404)
        var publicBeforeReactivate = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        publicBeforeReactivate.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 4. 呼叫管理員重新啟用端點 PUT /api/v1/products/{id}/reactivate
        var reactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/reactivate", null);
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. 開啟獨立 fresh DbContext，驗證 IsActive 已成功變更為 true，且識別碼與所有屬性保持不變
        await using (var db = CreateFreshDbContext())
        {
            var reloadedProduct = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloadedProduct.Should().NotBeNull();
            reloadedProduct!.IsActive.Should().BeTrue();
            reloadedProduct.Id.Should().Be(product.Id);
            reloadedProduct.Sku.Should().Be(sku);
            reloadedProduct.Price.Should().Be(originalPrice);
            reloadedProduct.Currency.Should().Be("TWD");
            reloadedProduct.Name.Should().Be($"Reactivation Product {runId}");
        }

        // 6. 重複呼叫重新啟用端點，驗證回傳 400 BadRequest (ProductErrors.AlreadyActive)
        var repeatedReactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/reactivate", null);
        repeatedReactivateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 7. 驗證公開匿名端點在 Reactivate 成功後，商品自動恢復可見性並成功返回 200 OK
        var publicAfterReactivate = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        publicAfterReactivate.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProduct = await publicAfterReactivate.Content.ReadFromJsonAsync<ProductResponse>();
        publicProduct.Should().NotBeNull();
        publicProduct!.Id.Should().Be(product.Id);
        publicProduct.IsActive.Should().BeTrue();
        publicProduct.Sku.Should().Be(sku);
        publicProduct.Price.Should().Be(originalPrice);
    }
}
