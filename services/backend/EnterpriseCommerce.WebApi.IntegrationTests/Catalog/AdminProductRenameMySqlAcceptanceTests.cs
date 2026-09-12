using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
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
public class AdminProductRenameMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public AdminProductRenameMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task AdminProduct_RealMySql_Rename_UpdatesCurrentNameAndPreservesAllInvariantsAndReflectsInPublicCatalog()
    {
        // 1. Seed Product with known values in real MySQL
        var runId = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"Original Product {runId}";
        var newName = $"Updated Product {runId}";
        var sku = $"SKU-RENAME-{runId}";
        var price = 199.99m;
        var currency = "TWD";

        var product = Product.Create(originalName, sku, price, currency).Value;
        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();

        // 2. Admin performs: PUT /api/v1/products/{id}/name
        var renameRequest = new UpdateProductNameRequest(newName);
        var renameResponse = await adminClient.PutAsJsonAsync($"/api/v1/products/{product.Id}/name", renameRequest);

        // 3. Response = 200 OK
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4 & 5. Reload from fresh DbContext (real MySQL) and verify invariants
        await using (var db = CreateFreshDbContext())
        {
            var reloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloaded.Should().NotBeNull();
            reloaded!.Name.Should().Be(newName);
            reloaded.Sku.Should().Be(sku);
            reloaded.Price.Should().Be(price);
            reloaded.Currency.Should().Be(currency);
            reloaded.IsActive.Should().BeTrue();
        }

        // 6. Anonymous public client reads GET /api/v1/products/{id} and observes new name
        var anonClient = _factory!.CreateClient();
        var publicResponse = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProduct = await publicResponse.Content.ReadFromJsonAsync<ProductResponse>();
        publicProduct.Should().NotBeNull();
        publicProduct!.Id.Should().Be(product.Id);
        publicProduct.Name.Should().Be(newName);
        publicProduct.Sku.Should().Be(sku);
        publicProduct.Price.Should().Be(price);
        publicProduct.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AdminProduct_RealMySql_RenameInactiveProduct_UpdatesNameAndPreservesInactivity()
    {
        // 1. Seed Inactive Product in real MySQL
        var runId = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"Inactive Product {runId}";
        var newName = $"Renamed Inactive Product {runId}";
        var sku = $"SKU-INACTIVE-RENAME-{runId}";
        var price = 50m;
        var currency = "TWD";

        var product = Product.Create(originalName, sku, price, currency).Value;
        product.Deactivate();

        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();

        // 2. Admin renames inactive product
        var renameRequest = new UpdateProductNameRequest(newName);
        var renameResponse = await adminClient.PutAsJsonAsync($"/api/v1/products/{product.Id}/name", renameRequest);
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Reload from fresh DbContext and verify
        await using (var db = CreateFreshDbContext())
        {
            var reloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloaded.Should().NotBeNull();
            reloaded!.Name.Should().Be(newName);
            reloaded.IsActive.Should().BeFalse();
            reloaded.Sku.Should().Be(sku);
            reloaded.Price.Should().Be(price);
        }
    }
}
