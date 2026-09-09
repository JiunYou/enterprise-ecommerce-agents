using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Inventory.ValueObjects;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Cart;
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
public class StockAwareCartAdmissionMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public StockAwareCartAdmissionMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

    private HttpClient CreateCustomerClient(Guid customerId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        return client;
    }

    // =========================================================================
    // RED CONTRACT A — ZERO STOCK ADD
    // =========================================================================
    [Fact]
    public async Task AddItem_ZeroAvailableStock_ShouldReturnBadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Zero Stock Product", "SKU-ZERO-" + Guid.NewGuid().ToString("N")[..8], 100m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        // AvailableQuantity is StockQuantity.Zero by default (0)

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert - Future behavior expects 400 Bad Request with InsufficientStock
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(InventoryErrors.InsufficientStock.Message);

        // Verify no cart item persisted in database
        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var order = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);

            if (order != null)
            {
                order.Items.Should().NotContain(i => i.ProductId == new ProductId(product.Id));
            }
        }
    }

    // =========================================================================
    // RED CONTRACT B — REQUEST EXCEEDS AVAILABLE
    // =========================================================================
    [Fact]
    public async Task AddItem_RequestExceedsAvailableStock_ShouldReturnBadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Limited Stock Product", "SKU-LIM-" + Guid.NewGuid().ToString("N")[..8], 150m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(2)); // Available = 2

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 3); // Requested 3 > Available 2

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(InventoryErrors.InsufficientStock.Message);

        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var order = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);

            if (order != null)
            {
                order.Items.Should().NotContain(i => i.ProductId == new ProductId(product.Id));
            }
        }
    }

    // =========================================================================
    // RED CONTRACT C — CUMULATIVE ADD BYPASS
    // =========================================================================
    [Fact]
    public async Task AddItem_CumulativeQuantityExceedsAvailableStock_ShouldReturnBadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Cumulative Product", "SKU-CUM-" + Guid.NewGuid().ToString("N")[..8], 50m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(3)); // Available = 3

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(50m, "USD"), 2); // Already in cart = 2

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.Orders.AddAsync(order);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 2); // Requested 2 + existing 2 = desired 4 > Available 3

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(InventoryErrors.InsufficientStock.Message);

        // Verify cart quantity remains 2
        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloadedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id);

            var item = reloadedOrder.Items.Single(i => i.ProductId == new ProductId(product.Id));
            item.Quantity.Should().Be(2);
        }
    }

    // =========================================================================
    // RED CONTRACT D — UPDATE QUANTITY BYPASS
    // =========================================================================
    [Fact]
    public async Task UpdateCartItemQuantity_ExceedsAvailableStock_ShouldReturnBadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Update Stock Product", "SKU-UPD-" + Guid.NewGuid().ToString("N")[..8], 80m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(2)); // Available = 2

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(80m, "USD"), 1); // Already in cart = 1

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.Orders.AddAsync(order);
            await dbContext.SaveChangesAsync();
        }

        var request = new UpdateCartItemQuantityRequest(3); // Requested absolute 3 > Available 2

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{product.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(InventoryErrors.InsufficientStock.Message);

        // Verify cart quantity remains 1
        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloadedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id);

            var item = reloadedOrder.Items.Single(i => i.ProductId == new ProductId(product.Id));
            item.Quantity.Should().Be(1);
        }
    }

    // =========================================================================
    // RED CONTRACT E — MISSING INVENTORY
    // =========================================================================
    [Fact]
    public async Task AddItem_MissingInventoryItem_ShouldReturnBadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("No Inventory Product", "SKU-NOINV-" + Guid.NewGuid().ToString("N")[..8], 200m, "USD").Value;
        // NO InventoryItem exists for this product

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert - Customer sees 400 Bad Request with InsufficientStock, NOT 404
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(InventoryErrors.InsufficientStock.Message);

        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var order = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.Status == OrderStatus.Pending);

            if (order != null)
            {
                order.Items.Should().NotContain(i => i.ProductId == new ProductId(product.Id));
            }
        }
    }

    // =========================================================================
    // CHARACTERIZATION — SUFFICIENT STOCK
    // =========================================================================
    [Fact]
    public async Task AddItem_WithSufficientStock_ShouldSucceed()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Valid Stock Product", "SKU-VAL-" + Guid.NewGuid().ToString("N")[..8], 100m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(5));

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 2);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 2);
    }

    [Fact]
    public async Task AddItem_CumulativeWithSufficientStock_ShouldSucceed()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Valid Cumulative Product", "SKU-VALCUM-" + Guid.NewGuid().ToString("N")[..8], 100m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(5));

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(100m, "USD"), 2);

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.Orders.AddAsync(order);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 2); // 2 + 2 = 4 <= 5

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 4);
    }

    [Fact]
    public async Task UpdateCartItemQuantity_WithSufficientStock_ShouldSucceed()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Valid Update Product", "SKU-VALUPD-" + Guid.NewGuid().ToString("N")[..8], 100m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(5));

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(100m, "USD"), 1);

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.Orders.AddAsync(order);
            await dbContext.SaveChangesAsync();
        }

        var request = new UpdateCartItemQuantityRequest(4); // 4 <= 5

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{product.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloadedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id);

            var item = reloadedOrder.Items.Single(i => i.ProductId == new ProductId(product.Id));
            item.Quantity.Should().Be(4);
        }
    }

    // =========================================================================
    // RED CONTRACT F — CUMULATIVE ADD OVERFLOW
    // =========================================================================
    [Fact]
    public async Task AddItem_CumulativeQuantityOverflow_ShouldReturnBadRequestAndPreserveCart()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var product = Product.Create("Overflow Product", "SKU-OVF-" + Guid.NewGuid().ToString("N")[..8], 10m, "USD").Value;
        var inventoryItem = InventoryItem.Create(new ProductReference(product.Id));
        inventoryItem.IncreaseStock(new StockQuantity(int.MaxValue)); // Available = int.MaxValue

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(product.Id), new Money(10m, "USD"), int.MaxValue); // Cart has int.MaxValue

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Products.AddAsync(product);
            await dbContext.InventoryItems.AddAsync(inventoryItem);
            await dbContext.Orders.AddAsync(order);
            await dbContext.SaveChangesAsync();
        }

        var request = new AddCartItemRequest(product.Id, 1); // Requested 1 -> int.MaxValue + 1

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert - Future contract requires 400 Bad Request with InsufficientStock
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(InventoryErrors.InsufficientStock.Message);

        // Verify persisted cart quantity remains int.MaxValue without mutation
        await using (var verifyContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            var reloadedOrder = await verifyContext.Orders
                .Include(o => o.Items)
                .FirstAsync(o => o.Id == order.Id);

            var item = reloadedOrder.Items.Single(i => i.ProductId == new ProductId(product.Id));
            item.Quantity.Should().Be(int.MaxValue);
        }
    }
}
