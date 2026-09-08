using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Inventory;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Inventory;

[Collection("IntegrationTests")]
public class AdminInventoryLifecycleMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public AdminInventoryLifecycleMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task AdminInventory_RealMySql_Lifecycle_MeetsAllAcceptanceCriteria()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-INV-{runId}";

        // 1. Seed Product P
        var product = Product.Create($"Inventory Test Product {runId}", sku, 100m, "TWD").Value;
        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        // 2. Seed InventoryItem for Product P (Available: 7, Reserved: 3)
        var productRef = new ProductReference(product.Id);
        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(10));
        var orderRef = new OrderReference(Guid.NewGuid());
        inventoryItem.ReserveStock(orderRef, new StockQuantity(3));

        await using (var db = CreateFreshDbContext())
        {
            db.InventoryItems.Add(inventoryItem);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();

        // 3. GET /api/v1/admin/products/{id}/inventory -> Available: 7, Reserved: 3
        var getResponse = await adminClient.GetAsync($"/api/v1/admin/products/{product.Id}/inventory");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inventoryData = await getResponse.Content.ReadFromJsonAsync<AdminInventoryResponse>();
        inventoryData.Should().NotBeNull();
        inventoryData!.ProductId.Should().Be(product.Id);
        inventoryData.AvailableQuantity.Should().Be(7);
        inventoryData.ReservedQuantity.Should().Be(3);

        // 4. POST increase +5 -> Available: 12, Reserved: 3
        var increaseResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/products/{product.Id}/inventory/increase",
            new AdjustInventoryStockRequest(5));
        increaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fresh DbContext 驗證持久化狀態
        await using (var db = CreateFreshDbContext())
        {
            var reloadedItem = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ProductReference == productRef);
            reloadedItem.Should().NotBeNull();
            reloadedItem!.AvailableQuantity.Value.Should().Be(12);
            reloadedItem.ReservedQuantity.Value.Should().Be(3);
        }

        // 5. POST decrease 4 -> Available: 8, Reserved: 3
        var decreaseResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/products/{product.Id}/inventory/decrease",
            new AdjustInventoryStockRequest(4));
        decreaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Fresh DbContext 驗證持久化狀態
        await using (var db = CreateFreshDbContext())
        {
            var reloadedItem = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ProductReference == productRef);
            reloadedItem.Should().NotBeNull();
            reloadedItem!.AvailableQuantity.Value.Should().Be(8);
            reloadedItem.ReservedQuantity.Value.Should().Be(3);
        }

        // 6. 嘗試扣減超過 AvailableQuantity (例如 9) -> 400 BadRequest (InsufficientStock)
        var excessiveDecreaseResponse = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/products/{product.Id}/inventory/decrease",
            new AdjustInventoryStockRequest(9));
        excessiveDecreaseResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Fresh DbContext 驗證失敗時庫存完全未變動 (Available: 8, Reserved: 3)
        await using (var db = CreateFreshDbContext())
        {
            var reloadedItem = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ProductReference == productRef);
            reloadedItem.Should().NotBeNull();
            reloadedItem!.AvailableQuantity.Value.Should().Be(8);
            reloadedItem.ReservedQuantity.Value.Should().Be(3);
        }

        // 7. 驗證資料庫唯一索引保證：不可重複為相同 Product 建立 InventoryItem
        await using (var db = CreateFreshDbContext())
        {
            var duplicateItem = InventoryItem.Create(productRef);
            db.InventoryItems.Add(duplicateItem);
            var saveDuplicate = async () => await db.SaveChangesAsync();
            await saveDuplicate.Should().ThrowAsync<DbUpdateException>();
        }

        // 8. 驗證真實 MySQL 樂觀併發機制 (Version 作為 ConcurrencyToken)
        await using (var db1 = CreateFreshDbContext())
        await using (var db2 = CreateFreshDbContext())
        {
            var itemInContext1 = await db1.InventoryItems.FirstAsync(i => i.ProductReference == productRef);
            var itemInContext2 = await db2.InventoryItems.FirstAsync(i => i.ProductReference == productRef);

            // Context 1 增加庫存並儲存成功
            itemInContext1.IncreaseStock(new StockQuantity(1));
            await db1.SaveChangesAsync();

            // Context 2 持有舊版本資訊進行異動，嘗試儲存必須觸發 DbUpdateConcurrencyException
            itemInContext2.IncreaseStock(new StockQuantity(1));
            var conflictAction = async () => await db2.SaveChangesAsync();
            await conflictAction.Should().ThrowAsync<DbUpdateConcurrencyException>(
                "在真實 MySQL 下，Version 欄位必須作為樂觀併發令牌拒絕過期的更新操作");
        }
    }
}
