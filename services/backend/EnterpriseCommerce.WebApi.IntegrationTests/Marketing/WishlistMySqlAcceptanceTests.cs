using EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlist;
using EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlistItemStatus;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Infrastructure.Persistence;
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

namespace EnterpriseCommerce.WebApi.IntegrationTests.Marketing;

[Collection("IntegrationTests")]
public class WishlistMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public WishlistMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

    [Fact]
    public async Task Wishlist_RealMySql_EndToEnd_Acceptance_And_CustomerIsolation()
    {
        // 1. Create Customer A & 2. Create Customer B
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        // 3. Create active Product A
        var productA = Product.Create("Active Product A", "SKU-WISHLIST-A-" + Guid.NewGuid().ToString("N")[..8], 199m, "TWD").Value;
        var inactiveProduct = Product.Create("Inactive Product", "SKU-INACTIVE-" + Guid.NewGuid().ToString("N")[..8], 99m, "TWD").Value;
        inactiveProduct.Deactivate();

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddRangeAsync(productA, inactiveProduct);
            await db.SaveChangesAsync();
        }

        var clientA = _factory!.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-a");
        clientA.DefaultRequestHeaders.Add("X-Test-User-Id", customerA.ToString());

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-b");
        clientB.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        // 4. Customer A adds Product A
        var addResponse = await clientA.PostAsync($"/api/v1/wishlist/items/{productA.Id}", null);
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. fresh DbContext verifies WishlistItem persisted
        // 6. CustomerId correct, 7. ProductId correct, 8. AddedAt populated
        await using (var db = CreateFreshDbContext())
        {
            var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.CustomerId == customerA && w.ProductId == productA.Id);
            item.Should().NotBeNull();
            item!.CustomerId.Should().Be(customerA);
            item.ProductId.Should().Be(productA.Id);
            item.AddedAt.Should().BeAfter(DateTimeOffset.MinValue);
        }

        // 9. Customer A GET Wishlist contains item
        var getResponseA = await clientA.GetAsync("/api/v1/wishlist?page=1&pageSize=25");
        getResponseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedA = await getResponseA.Content.ReadFromJsonAsync<WishlistResponse>();
        pagedA.Should().NotBeNull();
        pagedA!.TotalCount.Should().Be(1);
        pagedA.Items.Should().ContainSingle(i => i.ProductId == productA.Id);

        // 10. Customer B GET Wishlist does NOT contain item
        var getResponseB = await clientB.GetAsync("/api/v1/wishlist?page=1&pageSize=25");
        getResponseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedB = await getResponseB.Content.ReadFromJsonAsync<WishlistResponse>();
        pagedB.Should().NotBeNull();
        pagedB!.TotalCount.Should().Be(0);
        pagedB.Items.Should().BeEmpty();

        // 11. Customer B DELETE Product A cannot remove A's row
        var deleteResponseB = await clientB.DeleteAsync($"/api/v1/wishlist/items/{productA.Id}");
        deleteResponseB.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 12. fresh DbContext confirms A row still exists
        await using (var db = CreateFreshDbContext())
        {
            var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.CustomerId == customerA && w.ProductId == productA.Id);
            item.Should().NotBeNull();
        }

        // 13. duplicate Customer A add -> 409 Conflict
        var dupResponse = await clientA.PostAsync($"/api/v1/wishlist/items/{productA.Id}", null);
        dupResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 14. row count remains exactly one
        await using (var db = CreateFreshDbContext())
        {
            var count = await db.WishlistItems.CountAsync(w => w.CustomerId == customerA && w.ProductId == productA.Id);
            count.Should().Be(1);
        }

        // 15. Customer A remove -> success
        var deleteResponseA = await clientA.DeleteAsync($"/api/v1/wishlist/items/{productA.Id}");
        deleteResponseA.StatusCode.Should().Be(HttpStatusCode.OK);

        // 16. fresh DbContext confirms row removed
        await using (var db = CreateFreshDbContext())
        {
            var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.CustomerId == customerA && w.ProductId == productA.Id);
            item.Should().BeNull();
        }

        // 17. inactive Product cannot be newly added -> 404
        var addInactiveResponse = await clientA.PostAsync($"/api/v1/wishlist/items/{inactiveProduct.Id}", null);
        addInactiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 18. missing Product cannot be added -> 404
        var addMissingResponse = await clientA.PostAsync($"/api/v1/wishlist/items/{Guid.NewGuid()}", null);
        addMissingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Wishlist_Pagination_And_DeterministicOrdering_MySqlAcceptance()
    {
        var customerId = Guid.NewGuid();
        var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var p1 = Product.Create("Product 1", "SKU-PAGE-1-" + Guid.NewGuid().ToString("N")[..8], 10m, "TWD").Value;
        var p2 = Product.Create("Product 2", "SKU-PAGE-2-" + Guid.NewGuid().ToString("N")[..8], 20m, "TWD").Value;
        var p3 = Product.Create("Product 3", "SKU-PAGE-3-" + Guid.NewGuid().ToString("N")[..8], 30m, "TWD").Value;

        var baseTime = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);
        var item1 = WishlistItem.Create(Guid.NewGuid(), customerId, p1.Id, baseTime.AddHours(1)).Value;
        var item2 = WishlistItem.Create(Guid.NewGuid(), customerId, p2.Id, baseTime.AddHours(2)).Value; // 最新
        var item3 = WishlistItem.Create(Guid.NewGuid(), customerId, p3.Id, baseTime).Value; // 最舊

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddRangeAsync(p1, p2, p3);
            await db.WishlistItems.AddRangeAsync(item1, item2, item3);
            await db.SaveChangesAsync();
        }

        // 19. pagination totalCount correct & 20. ordering AddedAt DESC then Id DESC correct
        var page1Response = await client.GetAsync("/api/v1/wishlist?page=1&pageSize=2");
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page1 = await page1Response.Content.ReadFromJsonAsync<WishlistResponse>();
        page1.Should().NotBeNull();
        page1!.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page1.Items[0].ProductId.Should().Be(p2.Id); // item2 (12:00)
        page1.Items[1].ProductId.Should().Be(p1.Id); // item1 (11:00)

        var page2Response = await client.GetAsync("/api/v1/wishlist?page=2&pageSize=2");
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await page2Response.Content.ReadFromJsonAsync<WishlistResponse>();
        page2.Should().NotBeNull();
        page2!.TotalCount.Should().Be(3);
        page2.Items.Should().HaveCount(1);
        page2.Items[0].ProductId.Should().Be(p3.Id); // item3 (10:00)
    }

    [Fact]
    public async Task Wishlist_Membership_Status_RealMySql_EndToEnd_Acceptance()
    {
        // 1. Create Customer A & 2. Create Customer B
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        // 3. Create Product X
        var productX = Product.Create("Membership Test Product X", "SKU-MEMBERSHIP-X-" + Guid.NewGuid().ToString("N")[..8], 299m, "TWD").Value;

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddAsync(productX);
            await db.SaveChangesAsync();
        }

        var clientA = _factory!.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-a");
        clientA.DefaultRequestHeaders.Add("X-Test-User-Id", customerA.ToString());

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-b");
        clientB.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        // 4. Customer A adds Product X through existing Wishlist path
        var addResponseA = await clientA.PostAsync($"/api/v1/wishlist/items/{productX.Id}", null);
        addResponseA.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Fresh DbContext confirms one WishlistItems row
        await using (var db = CreateFreshDbContext())
        {
            var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.CustomerId == customerA && w.ProductId == productX.Id);
            item.Should().NotBeNull();
            item!.CustomerId.Should().Be(customerA);
            item.ProductId.Should().Be(productX.Id);

            var totalRows = await db.WishlistItems.CountAsync(w => w.ProductId == productX.Id);
            totalRows.Should().Be(1);
        }

        // 記錄呼叫 GET status 前的資料庫記錄狀態
        int preGetWishlistCount;
        int preGetProductCount;
        await using (var db = CreateFreshDbContext())
        {
            preGetWishlistCount = await db.WishlistItems.CountAsync();
            preGetProductCount = await db.Products.CountAsync();
        }

        // 6. Customer A status -> true
        var statusResponseA = await clientA.GetAsync($"/api/v1/wishlist/items/{productX.Id}/status");
        statusResponseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusA = await statusResponseA.Content.ReadFromJsonAsync<WishlistItemStatusResponse>();
        statusA.Should().NotBeNull();
        statusA!.ProductId.Should().Be(productX.Id);
        statusA.IsWishlisted.Should().BeTrue();

        // 7. Customer B status for Product X -> false
        var statusResponseB = await clientB.GetAsync($"/api/v1/wishlist/items/{productX.Id}/status");
        statusResponseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusB = await statusResponseB.Content.ReadFromJsonAsync<WishlistItemStatusResponse>();
        statusB.Should().NotBeNull();
        statusB!.ProductId.Should().Be(productX.Id);
        statusB.IsWishlisted.Should().BeFalse();

        // 8. Customer B must not receive information indicating another Customer owns the row
        var rawBContent = await statusResponseB.Content.ReadAsStringAsync();
        rawBContent.Should().NotContain(customerA.ToString());
        rawBContent.Should().NotContain("CustomerId", because: "Response contract must not expose CustomerId");

        // 12a. Verify no persistence mutation occurred from GET status requests themselves
        await using (var db = CreateFreshDbContext())
        {
            var postGetWishlistCount = await db.WishlistItems.CountAsync();
            var postGetProductCount = await db.Products.CountAsync();
            postGetWishlistCount.Should().Be(preGetWishlistCount);
            postGetProductCount.Should().Be(preGetProductCount);
        }

        // 9. Customer A removes Product X
        var deleteResponseA = await clientA.DeleteAsync($"/api/v1/wishlist/items/{productX.Id}");
        deleteResponseA.StatusCode.Should().Be(HttpStatusCode.OK);

        // 10. Fresh DbContext confirms row removed
        await using (var db = CreateFreshDbContext())
        {
            var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.CustomerId == customerA && w.ProductId == productX.Id);
            item.Should().BeNull();
        }

        // 11. Customer A status -> false
        var postRemoveStatusResponseA = await clientA.GetAsync($"/api/v1/wishlist/items/{productX.Id}/status");
        postRemoveStatusResponseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var postRemoveStatusA = await postRemoveStatusResponseA.Content.ReadFromJsonAsync<WishlistItemStatusResponse>();
        postRemoveStatusA.Should().NotBeNull();
        postRemoveStatusA!.ProductId.Should().Be(productX.Id);
        postRemoveStatusA.IsWishlisted.Should().BeFalse();

        // 12b. Final verification: no persistence mutation occurred from GET status requests
        await using (var db = CreateFreshDbContext())
        {
            var item = await db.WishlistItems.FirstOrDefaultAsync(w => w.CustomerId == customerA && w.ProductId == productX.Id);
            item.Should().BeNull();
        }
    }
}
