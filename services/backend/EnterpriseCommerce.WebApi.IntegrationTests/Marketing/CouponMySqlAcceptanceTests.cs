using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Cart;
using EnterpriseCommerce.WebApi.Contracts.Marketing;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Marketing;

[Collection("IntegrationTests")]
public class CouponMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CouponMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

    private HttpClient CreateClient(Guid userId, string role = "Customer")
    {
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        return client;
    }

    [Fact]
    public async Task MySql_CouponCode_UniqueConstraint_RejectsDuplicates()
    {
        await using var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions);
        var now = DateTimeOffset.UtcNow;
        var coupon1 = Coupon.Create(Guid.NewGuid(), "SAVE100", 100m, "USD", now, now.AddDays(7), now).Value;
        var coupon2 = Coupon.Create(Guid.NewGuid(), "SAVE100", 200m, "USD", now, now.AddDays(7), now).Value;

        dbContext.Coupons.Add(coupon1);
        await dbContext.SaveChangesAsync();

        dbContext.Coupons.Add(coupon2);
        var act = async () => await dbContext.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<EnterpriseCommerce.Application.Exceptions.CouponCodeConflictException>(
            "MySQL 必須拒絕重複的 Coupon.Code 並由基礎設施層轉譯為 CouponCodeConflictException");
        ex.Which.InnerException.Should().BeOfType<DbUpdateException>("底層例外必須為 EF Core 的 DbUpdateException");
    }

    [Fact]
    public async Task MySql_NonCouponUniqueViolation_DoesNotTranslateToCouponConflict()
    {
        await using var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions);
        var sku = $"SKU-TEST-{Guid.NewGuid():N}"[..20];
        var p1 = Product.Create("Product A", sku, 100m, "USD").Value;
        var p2 = Product.Create("Product B", sku, 200m, "USD").Value;

        dbContext.Products.Add(p1);
        await dbContext.SaveChangesAsync();

        dbContext.Products.Add(p2);
        var act = async () => await dbContext.SaveChangesAsync();

        var ex = await act.Should().ThrowAsync<DbUpdateException>("非 Coupon 實體重複違規必須保持為原始 DbUpdateException");
        ex.Which.Should().NotBeOfType<EnterpriseCommerce.Application.Exceptions.CouponCodeConflictException>();
    }

    [Fact]
    public async Task MySql_OtherCouponUniqueConstraint_Violation_DoesNotTranslateToCouponCodeConflict()
    {
        await using var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions);
        var now = DateTimeOffset.UtcNow;
        var testCurrency = "XYZ";

        // 清理測試資料
        var existingCoupons = await dbContext.Coupons.Where(c => c.Currency == testCurrency).ToListAsync();
        if (existingCoupons.Count != 0)
        {
            dbContext.Coupons.RemoveRange(existingCoupons);
            await dbContext.SaveChangesAsync();
        }

        // 建立測試專用唯一索引（非 Code 欄位）
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX UX_Test_Coupons_Currency ON Coupons (Currency);");
        }
        catch
        {
            // 索引若已存在則忽略
        }

        try
        {
            var coupon1 = Coupon.Create(Guid.NewGuid(), $"CODE1-{Guid.NewGuid():N}"[..15], 100m, testCurrency, now, now.AddDays(7), now).Value;
            var coupon2 = Coupon.Create(Guid.NewGuid(), $"CODE2-{Guid.NewGuid():N}"[..15], 200m, testCurrency, now, now.AddDays(7), now).Value;

            dbContext.Coupons.Add(coupon1);
            await dbContext.SaveChangesAsync();

            dbContext.Coupons.Add(coupon2);
            var act = async () => await dbContext.SaveChangesAsync();

            var ex = await act.Should().ThrowAsync<DbUpdateException>("非 Code 的 Coupon 唯一約束違規必須拋出原始 DbUpdateException");
            ex.Which.Should().NotBeOfType<EnterpriseCommerce.Application.Exceptions.CouponCodeConflictException>();
        }
        finally
        {
            // 清理測試專用索引
            try
            {
                await dbContext.Database.ExecuteSqlRawAsync("DROP INDEX UX_Test_Coupons_Currency ON Coupons;");
            }
            catch
            {
                // 忽略清理錯誤
            }
        }
    }




    [Fact]
    public async Task MySql_Coupon_CreateAndDeactivate_PersistsCorrectly()
    {
        var adminClient = CreateClient(Guid.NewGuid(), "Admin");
        var now = DateTimeOffset.UtcNow;
        var code = $"PROMO-{Guid.NewGuid():N}"[..15].ToUpperInvariant();

        var createRequest = new CreateCouponRequest(
            code,
            150m,
            "USD",
            now,
            now.AddDays(10));

        // 1. 建立 Coupon
        var createResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/coupons", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<CouponResponse>();
        created.Should().NotBeNull();
        created!.Code.Should().Be(code);
        created.IsActive.Should().BeTrue();

        // 驗證 DB
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var inDb = await dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == created.Id);
            inDb.Should().NotBeNull();
            inDb!.IsActive.Should().BeTrue();
            inDb.DiscountAmount.Should().Be(150m);
        }

        // 2. 停用 Coupon
        var deactivateResponse = await adminClient.PostAsync($"/api/v1/admin/coupons/{created.Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 驗證 DB 停用持久化
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var inDb = await dbContext.Coupons.FirstOrDefaultAsync(c => c.Id == created.Id);
            inDb.Should().NotBeNull();
            inDb!.IsActive.Should().BeFalse();
        }
    }

    [Fact]
    public async Task MySql_Cart_Apply_Remove_Mutation_ClearsCouponSnapshot()
    {
        var customerId = Guid.NewGuid();
        var client = CreateClient(customerId, "Customer");

        // 建立測試商品
        var product1Id = Guid.NewGuid();
        var product2Id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var couponCode = $"COUPON-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var product1 = Product.Create("商品一", "SKU-C1", 400m, "USD").Value;
            typeof(Product).GetProperty("Id")!.SetValue(product1, product1Id);
            var product2 = Product.Create("商品二", "SKU-C2", 300m, "USD").Value;
            typeof(Product).GetProperty("Id")!.SetValue(product2, product2Id);
            dbContext.Products.AddRange(product1, product2);

            var inv1 = EnterpriseCommerce.Domain.Inventory.InventoryItem.Create(new EnterpriseCommerce.Domain.Inventory.ValueObjects.ProductReference(product1Id));
            inv1.IncreaseStock(new EnterpriseCommerce.Domain.Inventory.ValueObjects.StockQuantity(100));
            var inv2 = EnterpriseCommerce.Domain.Inventory.InventoryItem.Create(new EnterpriseCommerce.Domain.Inventory.ValueObjects.ProductReference(product2Id));
            inv2.IncreaseStock(new EnterpriseCommerce.Domain.Inventory.ValueObjects.StockQuantity(100));
            dbContext.InventoryItems.AddRange(inv1, inv2);

            var coupon = Coupon.Create(Guid.NewGuid(), couponCode, 100m, "USD", now.AddDays(-1), now.AddDays(5), now).Value;
            dbContext.Coupons.Add(coupon);
            await dbContext.SaveChangesAsync();
        }

        // 1. 顧客新增品項到購物車 (400 USD * 2 = 800 USD)
        var addResp = await client.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest(product1Id, 2));
        addResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. 套用優惠券
        var applyResp = await client.PutAsJsonAsync("/api/v1/cart/coupon", new { code = couponCode });
        applyResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var appliedCart = await applyResp.Content.ReadFromJsonAsync<CartResponse>();
        appliedCart.Should().NotBeNull();
        appliedCart!.AppliedCouponCode.Should().Be(couponCode);
        appliedCart.DiscountAmount.Should().Be(100m);
        appliedCart.SubtotalAmount.Should().Be(800m);
        appliedCart.TotalAmount.Should().Be(700m);

        // 驗證 DB 確實持久化快照
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);
            order.Should().NotBeNull();
            order!.AppliedCouponCode.Should().Be(couponCode);
            order.AppliedCouponDiscountAmount.Should().Be(100m);
            order.AppliedCouponExpiresAt.Should().NotBeNull();
        }

        // 3. 變更品項數量 -> 購物車異動必須清除快照！
        var updateResp = await client.PutAsJsonAsync($"/api/v1/cart/items/{product1Id}", new UpdateCartItemQuantityRequest(3));
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 驗證 DB 中 snapshot 已被清除
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var order = await dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);
            order.Should().NotBeNull();
            order!.AppliedCouponCode.Should().BeNull();
            order.AppliedCouponDiscountAmount.Should().BeNull();
            order.AppliedCouponExpiresAt.Should().BeNull();
            order.DiscountAmount.Amount.Should().Be(0m);
            order.SubtotalAmount.Amount.Should().Be(1200m);
            order.TotalAmount.Amount.Should().Be(1200m);
        }

        // 4. 重新套用優惠券
        var reapplyResp = await client.PutAsJsonAsync("/api/v1/cart/coupon", new { code = couponCode });
        reapplyResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. 移除優惠券
        var removeResp = await client.DeleteAsync("/api/v1/cart/coupon");
        removeResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var removedCart = await removeResp.Content.ReadFromJsonAsync<CartResponse>();
        removedCart.Should().NotBeNull();
        removedCart!.AppliedCouponCode.Should().BeNull();
        removedCart.DiscountAmount.Should().Be(0m);
        removedCart.TotalAmount.Should().Be(1200m);

        // 驗證 DB 中 snapshot 已被清除
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);
            order.Should().NotBeNull();
            order!.AppliedCouponCode.Should().BeNull();
        }
    }

    [Fact]
    public async Task MySql_DatabaseSchema_NoMarketingForeignKeys()
    {
        await using var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions);
        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        // Coupons 表無外鍵
        var couponFkCount = await GetForeignKeyCountAsync(conn, "Coupons");
        couponFkCount.Should().Be(0, "Coupons 表絕對不得有任何外鍵");

        // Orders 表無外鍵指向 Coupons
        var orderToCouponFk = await HasForeignKeyBetweenTablesAsync(conn, "Orders", "Coupons");
        orderToCouponFk.Should().BeFalse("Orders 表絕對不得有指向 Coupons 表的外鍵");
    }

    private static async Task<int> GetForeignKeyCountAsync(DbConnection connection, string tableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = @TableName 
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'";
        var p = cmd.CreateParameter();
        p.ParameterName = "@TableName";
        p.Value = tableName;
        cmd.Parameters.Add(p);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private static async Task<bool> HasForeignKeyBetweenTablesAsync(DbConnection connection, string tableName, string referencedTableName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) 
            FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
            WHERE TABLE_SCHEMA = DATABASE() 
              AND TABLE_NAME = @TableName 
              AND REFERENCED_TABLE_NAME = @ReferencedTableName";
        var p1 = cmd.CreateParameter();
        p1.ParameterName = "@TableName";
        p1.Value = tableName;
        cmd.Parameters.Add(p1);

        var p2 = cmd.CreateParameter();
        p2.ParameterName = "@ReferencedTableName";
        p2.Value = referencedTableName;
        cmd.Parameters.Add(p2);

        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    [Fact]
    public async Task MySql_Coupon_ConcurrentDuplicate_DbRaceLoser_Returns409AndSingleRow()
    {
        var adminClient = CreateClient(Guid.NewGuid(), "Admin");
        var now = DateTimeOffset.UtcNow;
        var code = "WELCOME100";

        // 清理確保初始狀態無此 Code
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var existing = await dbContext.Coupons.Where(c => c.Code == code).ToListAsync();
            if (existing.Count != 0)
            {
                dbContext.Coupons.RemoveRange(existing);
                await dbContext.SaveChangesAsync();
            }
        }

        var createRequest = new CreateCouponRequest(
            code,
            100m,
            "USD",
            now,
            now.AddDays(10));

        // 1. 第一次建立成功
        var firstResponse = await adminClient.PostAsJsonAsync("/api/v1/admin/coupons", createRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. 第二個並發競態請求（模擬 pre-check 兩者同時通過，第二個直接抵達 MySQL UNIQUE(Coupons.Code) 約束）
        using var raceFactory = _factory!.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICouponRepository));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddScoped<ICouponRepository>(sp =>
                {
                    var db = sp.GetRequiredService<EnterpriseCommerceDbContext>();
                    var inner = new EnterpriseCommerce.Infrastructure.Persistence.Marketing.CouponRepository(db);
                    return new ConcurrencySimulatingCouponRepository(inner);
                });
            });
        });

        var raceClient = raceFactory.CreateClient();
        raceClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        raceClient.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        raceClient.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var secondResponse = await raceClient.PostAsJsonAsync("/api/v1/admin/coupons", createRequest);

        // 斷言：必須為 409 Conflict，而非 500
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var responseContent = await secondResponse.Content.ReadAsStringAsync();
        responseContent.Should().NotContain("DbUpdateException");
        responseContent.Should().NotContain("1062");
        responseContent.Should().NotContain("IX_Coupons_Code");
        responseContent.Should().NotContain("MySqlException");

        // 3. 資料庫中 WELCOME100 剛好只有 1 筆記錄
        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var count = await dbContext.Coupons.CountAsync(c => c.Code == code);
            count.Should().Be(1);
        }
    }

    private sealed class ConcurrencySimulatingCouponRepository : ICouponRepository
    {
        private readonly ICouponRepository _inner;

        public ConcurrencySimulatingCouponRepository(ICouponRepository inner)
        {
            _inner = inner;
        }

        public Task<Coupon?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => _inner.GetByNormalizedCodeAsync(normalizedCode, cancellationToken);

        public Task<Coupon?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<Coupon>> GetAllAsync(CancellationToken cancellationToken = default)
            => _inner.GetAllAsync(cancellationToken);

        public Task<bool> ExistsByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
        {
            // 模擬並發競態：兩請求同時通過預檢查，回傳 false
            return Task.FromResult(false);
        }

        public void Add(Coupon coupon) => _inner.Add(coupon);
    }
}

