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
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Catalog;

[Collection("IntegrationTests")]
public class ProductImageUrlMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public ProductImageUrlMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task ProductImageUrl_RealMySql_EndToEndLifecycle_SatisfiesAllSection35Requirements()
    {
        // 1. Product begins ImageUrl empty.
        var runId = Guid.NewGuid().ToString("N")[..8];
        var originalName = $"Product {runId}";
        var sku = $"SKU-IMG-{runId}";
        var price = 399.50m;
        var currency = "TWD";
        var description = $"Description for product {runId}";

        var product = Product.Create(originalName, sku, price, currency).Value;
        product.UpdateDescription(description).IsSuccess.Should().BeTrue();
        product.ImageUrl.Should().Be(string.Empty);

        await using (var db = CreateFreshDbContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var adminClient = CreateAdminClient();
        var anonClient = CreateAnonymousClient();

        // 2. Admin PUT valid HTTPS ImageUrl.
        var targetUrl = "  https://cdn.example.com/images/product-hero.png  ";
        var expectedNormalizedUrl = "https://cdn.example.com/images/product-hero.png";
        var updateRequest = new UpdateProductImageUrlRequest(targetUrl);
        var updateResponse = await adminClient.PutAsJsonAsync($"/api/v1/products/{product.Id}/image-url", updateRequest);

        // 3. response 200.
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. fresh DbContext reload.
        await using (var db = CreateFreshDbContext())
        {
            var reloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            reloaded.Should().NotBeNull();

            // 5. URL persisted normalized.
            reloaded!.ImageUrl.Should().Be(expectedNormalizedUrl);

            // 6. Name invariant.
            reloaded.Name.Should().Be(originalName);

            // 7. SKU invariant.
            reloaded.Sku.Should().Be(sku);

            // 8. Price invariant.
            reloaded.Price.Should().Be(price);

            // 9. Currency invariant.
            reloaded.Currency.Should().Be(currency);

            // 10. IsActive invariant.
            reloaded.IsActive.Should().BeTrue();

            // 11. Description invariant.
            reloaded.Description.Should().Be(description);
        }

        // 12. public Product list returns ImageUrl.
        var listResponse = await anonClient.GetAsync($"/api/v1/products?searchTerm={sku}");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedList = await listResponse.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        pagedList.Should().NotBeNull();
        pagedList!.Items.Should().ContainSingle(p => p.Id == product.Id && p.ImageUrl == expectedNormalizedUrl);

        // 13. public Product detail returns ImageUrl.
        var publicDetailResponse = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        publicDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicDetail = await publicDetailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        publicDetail.Should().NotBeNull();
        publicDetail!.Id.Should().Be(product.Id);
        publicDetail.ImageUrl.Should().Be(expectedNormalizedUrl);
        publicDetail.Description.Should().Be(description);

        // 14. Admin detail returns ImageUrl.
        var adminDetailResponse = await adminClient.GetAsync($"/api/v1/admin/products/{product.Id}");
        adminDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminDetail = await adminDetailResponse.Content.ReadFromJsonAsync<ProductDetailResponse>();
        adminDetail.Should().NotBeNull();
        adminDetail!.Id.Should().Be(product.Id);
        adminDetail.ImageUrl.Should().Be(expectedNormalizedUrl);

        // 15. inactive Product can update ImageUrl.
        var deactivateResponse = await adminClient.PutAsync($"/api/v1/products/{product.Id}/deactivate", null);
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 16. public inactive detail existing visibility semantics unchanged (returns 404 for anonymous).
        var anonInactiveResponse = await anonClient.GetAsync($"/api/v1/products/{product.Id}");
        anonInactiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Update image on inactive product
        var inactiveImageUrl = "https://cdn.example.com/images/inactive-variant.jpg";
        var updateInactiveResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/image-url",
            new UpdateProductImageUrlRequest(inactiveImageUrl));
        updateInactiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = CreateFreshDbContext())
        {
            var inactiveReloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            inactiveReloaded.Should().NotBeNull();
            inactiveReloaded!.ImageUrl.Should().Be(inactiveImageUrl);
            inactiveReloaded.IsActive.Should().BeFalse();
        }

        // 17. clear with whitespace persists empty string.
        var clearResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/image-url",
            new UpdateProductImageUrlRequest("   "));
        clearResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = CreateFreshDbContext())
        {
            var clearedReloaded = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            clearedReloaded.Should().NotBeNull();
            clearedReloaded!.ImageUrl.Should().Be(string.Empty);
            clearedReloaded.IsActive.Should().BeFalse();
        }

        // 18. invalid HTTP URL does not mutate previous value.
        // First set a known image
        var presetUrl = "https://cdn.example.com/images/preset.png";
        var presetResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/image-url",
            new UpdateProductImageUrlRequest(presetUrl));
        presetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Attempt invalid HTTP (non-https) URL
        var invalidResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/products/{product.Id}/image-url",
            new UpdateProductImageUrlRequest("http://insecure.example.com/img.jpg"));
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var db = CreateFreshDbContext())
        {
            var preserved = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == product.Id);
            preserved.Should().NotBeNull();
            preserved!.ImageUrl.Should().Be(presetUrl);
        }
    }
}
