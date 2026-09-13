using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
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
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Catalog;

[Collection("IntegrationTests")]
public class ProductAvailabilityMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public ProductAvailabilityMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task ProductAvailability_EndToEnd_MySqlAcceptance()
    {
        // 1. Create active Product
        var product = Product.Create("Acceptance Product", "SKU-AVAIL-" + Guid.NewGuid().ToString("N")[..8], 150m, "TWD").Value;

        // 2. Create InventoryItem
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));

        // 3. Increase available stock to known value, e.g. 8
        inventoryItem.IncreaseStock(new StockQuantity(8));

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddAsync(product);
            await db.InventoryItems.AddAsync(inventoryItem);
            await db.SaveChangesAsync();
        }

        var client = _factory!.CreateClient();

        // 4. Anonymous availability endpoint returns 200
        var response = await client.GetAsync($"/api/v1/products/{product.Id}/availability");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. availableQuantity == 8 & 6. inStock == true
        var availability = await response.Content.ReadFromJsonAsync<ProductAvailabilityResponse>();
        availability.Should().NotBeNull();
        availability!.ProductId.Should().Be(product.Id);
        availability.AvailableQuantity.Should().Be(8);
        availability.InStock.Should().BeTrue();

        // 7. Response does not expose reserved quantity
        var rawJson = await response.Content.ReadAsStringAsync();
        rawJson.ToLowerInvariant().Should().NotContain("reservedquantity");
        rawJson.ToLowerInvariant().Should().NotContain("reservations");
        rawJson.ToLowerInvariant().Should().NotContain("orderreference");
        rawJson.ToLowerInvariant().Should().NotContain("inventoryid");

        // 8. Reserve some stock through actual Domain/persistence-compatible path
        var orderRef = new OrderReference(Guid.NewGuid());
        await using (var db = CreateFreshDbContext())
        {
            var itemToReserve = await db.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(product.Id));
            itemToReserve.ReserveStock(orderRef, new StockQuantity(3));
            await db.SaveChangesAsync();
        }

        // 9. Fresh DbContext confirms AvailableQuantity decreases
        await using (var freshDb = CreateFreshDbContext())
        {
            var verifiedItem = await freshDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(product.Id));
            verifiedItem.AvailableQuantity.Value.Should().Be(5);
            verifiedItem.ReservedQuantity.Value.Should().Be(3);
        }

        // 10. Public availability returns new AvailableQuantity (5)
        var updatedResponse = await client.GetAsync($"/api/v1/products/{product.Id}/availability");
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedAvailability = await updatedResponse.Content.ReadFromJsonAsync<ProductAvailabilityResponse>();
        updatedAvailability.Should().NotBeNull();
        updatedAvailability!.AvailableQuantity.Should().Be(5);
        updatedAvailability.InStock.Should().BeTrue();

        // 11. Zero AvailableQuantity returns inStock=false
        await using (var db = CreateFreshDbContext())
        {
            var itemToDeplete = await db.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(product.Id));
            // Reserve remaining 5
            itemToDeplete.ReserveStock(new OrderReference(Guid.NewGuid()), new StockQuantity(5));
            await db.SaveChangesAsync();
        }

        await using (var freshDb = CreateFreshDbContext())
        {
            var zeroItem = await freshDb.InventoryItems.FirstAsync(i => i.ProductReference == new ProductReference(product.Id));
            zeroItem.AvailableQuantity.Value.Should().Be(0);
        }

        var zeroResponse = await client.GetAsync($"/api/v1/products/{product.Id}/availability");
        zeroResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var zeroAvailability = await zeroResponse.Content.ReadFromJsonAsync<ProductAvailabilityResponse>();
        zeroAvailability.Should().NotBeNull();
        zeroAvailability!.AvailableQuantity.Should().Be(0);
        zeroAvailability.InStock.Should().BeFalse();

        // 12. Inactive Product returns 404
        var inactiveProduct = Product.Create("Inactive Product", "SKU-INACT-" + Guid.NewGuid().ToString("N")[..8], 200m, "TWD").Value;
        inactiveProduct.Deactivate();
        var inactiveInventory = InventoryItem.Create(new ProductReference(inactiveProduct.Id));
        inactiveInventory.IncreaseStock(new StockQuantity(10));

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddAsync(inactiveProduct);
            await db.InventoryItems.AddAsync(inactiveInventory);
            await db.SaveChangesAsync();
        }

        var inactiveResponse = await client.GetAsync($"/api/v1/products/{inactiveProduct.Id}/availability");
        inactiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 13. Missing Inventory for otherwise visible Product fails closed and does not fabricate zero stock
        var noInventoryProduct = Product.Create("No Inventory Product", "SKU-NOINV-" + Guid.NewGuid().ToString("N")[..8], 300m, "TWD").Value;
        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddAsync(noInventoryProduct);
            await db.SaveChangesAsync();
        }

        var noInventoryResponse = await client.GetAsync($"/api/v1/products/{noInventoryProduct.Id}/availability");
        noInventoryResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
