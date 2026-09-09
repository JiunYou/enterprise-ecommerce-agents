using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

[Collection("IntegrationTests")]
public class CheckoutProductEligibilityMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CheckoutProductEligibilityMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

    private HttpClient CreateCustomerClient(Guid customerId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        return client;
    }

    private static SubmitOrderRequest CreateValidSubmitOrderRequest() =>
        new(new ShippingAddressRequest(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "123 Main St",
            "Apt 4B"));

    // =========================================================================
    // RED A: INACTIVE SINGLE PRODUCT
    // =========================================================================
    [Fact]
    public async Task SubmitOrder_WithInactiveProduct_ShouldRejectWith400AndNotActive_AndNotReserveInventory()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Inactive Test Product", $"SKU-INACT-{Guid.NewGuid():N}", 100m, "USD").Value;
        var deactivateResult = product.Deactivate();
        deactivateResult.IsSuccess.Should().BeTrue();

        var productRef = new ProductReference(product.Id);
        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(50));

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(100m, "USD"), 2);

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Products.Add(product);
            dbContext.InventoryItems.Add(inventoryItem);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateCustomerClient(customerId);
        var request = CreateValidSubmitOrderRequest();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);

        // Assert (Future frozen contract expectation: HTTP 400 Bad Request with ProductErrors.NotActive)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(ProductErrors.NotActive.Message);

        // Assert: Persistence must remain Pending, SubmittedAt null, and ReservedQuantity 0
        await using (var verifyContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            persistedOrder.Status.Should().Be(OrderStatus.Pending);
            persistedOrder.SubmittedAt.Should().BeNull();

            var persistedInventory = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);

            persistedInventory.ReservedQuantity.Value.Should().Be(0);
            persistedInventory.AvailableQuantity.Value.Should().Be(50);
            persistedInventory.Reservations.Should().BeEmpty();
        }
    }

    // =========================================================================
    // RED B: MULTI-ITEM ONE INACTIVE
    // =========================================================================
    [Fact]
    public async Task SubmitOrder_WithMultiItemsWhereOneIsInactive_ShouldRejectWith400AndNotActive_AndHaveNoPartialReservations()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        var productA = Product.Create("Product A Active", $"SKU-A-{Guid.NewGuid():N}", 50m, "USD").Value;
        var productB = Product.Create("Product B Inactive", $"SKU-B-{Guid.NewGuid():N}", 75m, "USD").Value;
        productB.Deactivate().IsSuccess.Should().BeTrue();
        var productC = Product.Create("Product C Active", $"SKU-C-{Guid.NewGuid():N}", 120m, "USD").Value;

        var refA = new ProductReference(productA.Id);
        var refB = new ProductReference(productB.Id);
        var refC = new ProductReference(productC.Id);

        var invA = InventoryItem.Create(refA);
        invA.IncreaseStock(new StockQuantity(50));
        var invB = InventoryItem.Create(refB);
        invB.IncreaseStock(new StockQuantity(50));
        var invC = InventoryItem.Create(refC);
        invC.IncreaseStock(new StockQuantity(50));

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(productA.Id), new Money(50m, "USD"), 1);
        order.AddItem(new ProductId(productB.Id), new Money(75m, "USD"), 1);
        order.AddItem(new ProductId(productC.Id), new Money(120m, "USD"), 1);

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Products.AddRange(productA, productB, productC);
            dbContext.InventoryItems.AddRange(invA, invB, invC);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateCustomerClient(customerId);
        var request = CreateValidSubmitOrderRequest();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);

        // Assert (Future frozen contract: HTTP 400 Bad Request with ProductErrors.NotActive)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(ProductErrors.NotActive.Message);

        // Assert: Persistence must remain Pending with NO partial inventory reservations
        await using (var verifyContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            persistedOrder.Status.Should().Be(OrderStatus.Pending);
            persistedOrder.SubmittedAt.Should().BeNull();

            var persistedA = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == refA);
            persistedA.ReservedQuantity.Value.Should().Be(0);
            persistedA.Reservations.Should().BeEmpty();

            var persistedB = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == refB);
            persistedB.ReservedQuantity.Value.Should().Be(0);
            persistedB.Reservations.Should().BeEmpty();

            var persistedC = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == refC);
            persistedC.ReservedQuantity.Value.Should().Be(0);
            persistedC.Reservations.Should().BeEmpty();
        }
    }

    // =========================================================================
    // RED C: MISSING PRODUCT
    // =========================================================================
    [Fact]
    public async Task SubmitOrder_WithMissingProductInCatalog_ShouldRejectWith404AndNotFound_AndNotReserveInventory()
    {
        // Arrange:
        // OrderItem references non-existent ProductId X in Products table.
        // Inventory DOES exist for X to prove Product eligibility failure independently of Inventory.
        var customerId = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();
        var productRef = new ProductReference(missingProductId);

        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(50));

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(missingProductId), new Money(100m, "USD"), 2);

        await using (var dbContext = CreateFreshDbContext())
        {
            // Note: product is intentionally NOT added to dbContext.Products
            dbContext.InventoryItems.Add(inventoryItem);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateCustomerClient(customerId);
        var request = CreateValidSubmitOrderRequest();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);

        // Assert (Future frozen contract expectation: HTTP 404 Not Found with ProductErrors.NotFound)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(ProductErrors.NotFound.Message);

        // Assert: Persistence must remain Pending, SubmittedAt null, and ReservedQuantity 0
        await using (var verifyContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            persistedOrder.Status.Should().Be(OrderStatus.Pending);
            persistedOrder.SubmittedAt.Should().BeNull();

            var persistedInventory = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);

            persistedInventory.ReservedQuantity.Value.Should().Be(0);
            persistedInventory.AvailableQuantity.Value.Should().Be(50);
            persistedInventory.Reservations.Should().BeEmpty();
        }
    }

    // =========================================================================
    // CHARACTERIZATION: ACTIVE PRODUCT SUCCESS
    // =========================================================================
    [Fact]
    public async Task SubmitOrder_WithActiveProduct_Characterization_PreservesSuccessfulSubmission()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var product = Product.Create("Active Product", $"SKU-ACT-{Guid.NewGuid():N}", 100m, "USD").Value;
        var productRef = new ProductReference(product.Id);

        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(50));

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(100m, "USD"), 2);

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Products.Add(product);
            dbContext.InventoryItems.Add(inventoryItem);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = CreateCustomerClient(customerId);
        var request = CreateValidSubmitOrderRequest();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);

        // Assert: Existing successful behavior preserved (HTTP 200 OK)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Verified in MySQL persistence
        await using (var verifyContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            persistedOrder.Status.Should().Be(OrderStatus.Submitted);
            persistedOrder.SubmittedAt.Should().NotBeNull();

            var persistedInventory = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);

            persistedInventory.ReservedQuantity.Value.Should().Be(2);
            persistedInventory.AvailableQuantity.Value.Should().Be(48);
            persistedInventory.Reservations.Should().ContainSingle(r => r.OrderReference.Value == order.Id.Value);
        }
    }
}
