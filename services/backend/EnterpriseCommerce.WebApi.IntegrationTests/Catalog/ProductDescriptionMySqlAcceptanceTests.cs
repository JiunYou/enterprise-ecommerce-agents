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
public class ProductDescriptionMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public ProductDescriptionMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task ProductDescription_RealMySql_EndToEndLifecycle_SatisfiesAllSection35Requirements()
    {
        // 1. Product exists with empty Description
        var runId = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"Product {runId}";
        var sku = $"SKU-DESC-{runId}";
        var price = 299.99m;
        var currency = "TWD";

        var product = Product.Create(originalName, sku, price, currency).Value;
        product.Description.Should().Be(string.Empty);

        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();
        var anonClient = CreateAnonymousClient();

        // 2. Admin updates Description
        var newDescription = $"This is an exceptional enterprise product {runId}.\nLine two details.";
        var updateRequest = new UpdateProductDescriptionRequest(newDescription);
        var updateResponse = await adminClient.PutAsJsonAsync($"/api/v1/products/{product.Id}/description", updateRequest);

        // 3. API returns 200
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Reload using fresh DbContext
        await using (var db = CreateFreshDbContext())
        {
            var reloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloaded.Should().NotBeNull();

            // 5. Description persists
            reloaded!.Description.Should().Be(newDescription.Trim());

            // 6. Name unchanged
            reloaded.Name.Should().Be(originalName);

            // 7. SKU unchanged
            reloaded.Sku.Should().Be(sku);

            // 8. Price unchanged
            reloaded.Price.Should().Be(price);

            // 9. Currency unchanged
            reloaded.Currency.Should().Be(currency);

            // 10. IsActive unchanged
            reloaded.IsActive.Should().BeTrue();
        }

        // 11. Public Product Detail returns Description
        var publicResponse = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicDetail = await publicResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        publicDetail.Should().NotBeNull();
        publicDetail!.Id.Should().Be(product.Id);
        publicDetail.Description.Should().Be(newDescription.Trim());
        publicDetail.Name.Should().Be(originalName);
        publicDetail.Sku.Should().Be(sku);
        publicDetail.Price.Should().Be(price);
        publicDetail.Currency.Should().Be(currency);
        publicDetail.IsActive.Should().BeTrue();

        // Deactivate product to test inactive product behavior
        var deactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 12. Customer-style anonymous public Product Detail remains inaccessible for inactive Product according to existing behavior
        var anonInactiveResponse = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        anonInactiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 13. Admin may update Description on inactive Product
        var inactiveDescription = $"Updated inactive product description {runId}";
        var updateInactiveResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/description",
            new UpdateProductDescriptionRequest(inactiveDescription));
        updateInactiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 14. Admin Product Detail reads Description
        var adminDetailResponse = await adminClient.GetAsync($"/api/v1/admin/products/{product.Id}");
        adminDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminDetail = await adminDetailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        adminDetail.Should().NotBeNull();
        adminDetail!.Id.Should().Be(product.Id);
        adminDetail.Description.Should().Be(inactiveDescription.Trim());
        adminDetail.IsActive.Should().BeFalse();

        // 15. Clear Description persists as empty string
        var clearResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/description",
            new UpdateProductDescriptionRequest("   "));
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = CreateFreshDbContext())
        {
            var clearedProduct = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            clearedProduct.Should().NotBeNull();
            clearedProduct!.Description.Should().Be(string.Empty);
            clearedProduct.IsActive.Should().BeFalse();
            clearedProduct.Name.Should().Be(originalName);
            clearedProduct.Sku.Should().Be(sku);
        }

        // Verify Admin detail confirms cleared description as string.Empty
        var adminClearedDetailResponse = await adminClient.GetAsync($"/api/v1/admin/products/{product.Id}");
        adminClearedDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminClearedDetail = await adminClearedDetailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        adminClearedDetail.Should().NotBeNull();
        adminClearedDetail!.Description.Should().Be(string.Empty);
    }
}
