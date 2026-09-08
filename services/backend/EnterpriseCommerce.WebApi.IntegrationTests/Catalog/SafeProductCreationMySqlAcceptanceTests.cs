using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Catalog;
using EnterpriseCommerce.WebApi.Contracts.Inventory;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Catalog;

[Collection("IntegrationTests")]
public class SafeProductCreationMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public SafeProductCreationMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task CreateProduct_RealMySql_Success_AtomicallyCreatesProductAndInventoryItem()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-SAFE-CREATE-{runId}";
        var name = $"Safe Product {runId}";
        var price = 199m;
        var currency = "TWD";
        var initialStock = 15;

        var adminClient = CreateAdminClient();
        var request = new CreateProductRequest(name, sku, price, currency, initialStock);

        // Act
        var response = await adminClient.PostAsJsonAsync("/api/v1/Products", request);

        // Assert HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdProductId = await response.Content.ReadFromJsonAsync<Guid>();
        createdProductId.Should().NotBeEmpty();

        // Assert fresh DbContext
        await using (var db = CreateFreshDbContext())
        {
            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == createdProductId);
            product.Should().NotBeNull();
            product!.Name.Should().Be(name);
            product.Sku.Should().Be(sku);
            product.Price.Should().Be(price);
            product.Currency.Should().Be(currency);
            product.IsActive.Should().BeTrue();

            var productRef = new ProductReference(createdProductId);
            var inventoryItem = await db.InventoryItems.AsNoTracking().FirstOrDefaultAsync(i => i.ProductReference == productRef);
            inventoryItem.Should().NotBeNull();
            inventoryItem!.AvailableQuantity.Value.Should().Be(initialStock);
            inventoryItem.ReservedQuantity.Value.Should().Be(0);
        }

        // Assert Admin Inventory GET
        var inventoryResponse = await adminClient.GetAsync($"/api/v1/admin/products/{createdProductId}/inventory");
        inventoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var inventoryData = await inventoryResponse.Content.ReadFromJsonAsync<AdminInventoryResponse>();
        inventoryData.Should().NotBeNull();
        inventoryData!.ProductId.Should().Be(createdProductId);
        inventoryData.AvailableQuantity.Should().Be(initialStock);
        inventoryData.ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task CreateProduct_RealMySql_WhenInvalidInitialStock_ReturnsBadRequestAndPersistsNothing()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-SAFE-INVALID-{runId}";
        var name = $"Invalid Stock Product {runId}";
        var price = 199m;
        var currency = "TWD";
        var invalidInitialStock = 0; // Contract requires > 0

        var adminClient = CreateAdminClient();
        var request = new CreateProductRequest(name, sku, price, currency, invalidInitialStock);

        // Act
        var response = await adminClient.PostAsJsonAsync("/api/v1/Products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify fresh DbContext has 0 products and 0 inventories
        await using (var db = CreateFreshDbContext())
        {
            var productCount = await db.Products.CountAsync(p => p.Sku == sku);
            productCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task CreateProduct_RealMySql_WhenInventoryInsertFails_RollsBackProductCreationAtomically()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-SAFE-FAIL-{runId}";
        var name = $"Fail Test Product {runId}";
        var price = 100m;
        var currency = "TWD";
        var initialStock = 99999;

        await using (var db = CreateFreshDbContext())
        {
            // Add deterministic check constraint to reject initialStock = 99999
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE InventoryItems ADD CONSTRAINT chk_test_fail_inventory CHECK (AvailableQuantity != 99999);");
        }

        try
        {
            var adminClient = CreateAdminClient();
            var request = new CreateProductRequest(name, sku, price, currency, initialStock);

            // Act
            var response = await adminClient.PostAsJsonAsync("/api/v1/Products", request);

            // Assert: Expect server error due to DB constraint failure
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

            // Verify atomic rollback in fresh DbContext: Product row count for attempted SKU must be 0
            await using (var db = CreateFreshDbContext())
            {
                var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Sku == sku);
                product.Should().BeNull();
            }
        }
        finally
        {
            await using var db = CreateFreshDbContext();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE InventoryItems DROP CHECK chk_test_fail_inventory;");
        }
    }

    [Fact]
    public async Task CreateProduct_RealMySql_DuplicateSku_RejectsSafelyAndLeavesNoPartialInventory()
    {
        var runId = Guid.NewGuid().ToString("N")[..8];
        var sku = $"SKU-SAFE-DUP-{runId}";
        var name = $"First Product {runId}";
        var price = 100m;
        var currency = "TWD";
        var initialStock = 10;

        var adminClient = CreateAdminClient();
        var request = new CreateProductRequest(name, sku, price, currency, initialStock);

        // 1. First creation
        var firstResponse = await adminClient.PostAsJsonAsync("/api/v1/Products", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstProductId = await firstResponse.Content.ReadFromJsonAsync<Guid>();

        // 2. Second creation with duplicate SKU
        var duplicateRequest = new CreateProductRequest($"Duplicate {name}", sku, 200m, currency, 20);
        var secondResponse = await adminClient.PostAsJsonAsync("/api/v1/Products", duplicateRequest);

        // Assert: safely rejected with Conflict (409)
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Assert data integrity: exactly one product, exactly one inventory item
        await using (var db = CreateFreshDbContext())
        {
            var products = await db.Products.AsNoTracking().Where(p => p.Sku == sku).ToListAsync();
            products.Should().HaveCount(1);
            products[0].Id.Should().Be(firstProductId);

            var firstProductRef = new ProductReference(firstProductId);
            var inventories = await db.InventoryItems.AsNoTracking().Where(i => i.ProductReference == firstProductRef).ToListAsync();
            inventories.Should().HaveCount(1);
            inventories[0].AvailableQuantity.Value.Should().Be(initialStock);
        }
    }
}
