using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class OrderSubmissionMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public OrderSubmissionMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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

        // WebApplicationFactory retaining real MediatR, real Handlers, real UnitOfWork, real MySQL DbContext!
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

    private static SubmitOrderRequest CreateValidSubmitOrderRequest() =>
        new(new ShippingAddressRequest(
            "Test Recipient",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "123 Test St",
            "Floor 4"));

    [Fact]
    public async Task SubmitOrder_RealMySql_Success_TransitionsToSubmitted_ReservesInventory_ClearsCart_AndRemainsRetrievable()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var productRef = new ProductReference(productId);
        int initialStock = 50;
        int orderQuantity = 2;

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(productId), new Money(100m, "USD"), orderQuantity);

        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(initialStock));

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.InventoryItems.Add(inventoryItem);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        // Mechanically verify state BEFORE submission
        await using (var verifyBeforeContext = CreateFreshDbContext())
        {
            var orderBefore = await verifyBeforeContext.Orders.SingleAsync(o => o.Id == order.Id);
            orderBefore.Status.Should().Be(OrderStatus.Pending);
            orderBefore.ShippingAddress.Should().BeNull();

            var inventoryBefore = await verifyBeforeContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);
            inventoryBefore.AvailableQuantity.Value.Should().Be(initialStock);
            inventoryBefore.ReservedQuantity.Value.Should().Be(0);
            inventoryBefore.Reservations.Should().BeEmpty();
        }

        // Act 1: Submit the order via real HTTP WebApi endpoint with valid ShippingAddress
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = CreateValidSubmitOrderRequest();
        var submitResponse = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert 1: Verify persisted state in MySQL using a FRESH DbContext
        await using (var verifyAfterContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyAfterContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            persistedOrder.Status.Should().Be(OrderStatus.Submitted);
            persistedOrder.SubmittedAt.Should().NotBeNull();
            persistedOrder.Status.Should().NotBe(OrderStatus.Paid);

            // Verify shipping address snapshot persistence in MySQL
            persistedOrder.ShippingAddress.Should().NotBeNull();
            persistedOrder.ShippingAddress!.RecipientName.Should().Be("Test Recipient");
            persistedOrder.ShippingAddress.Phone.Should().Be("0912345678");
            persistedOrder.ShippingAddress.CountryCode.Should().Be("TW");
            persistedOrder.ShippingAddress.PostalCode.Should().Be("100");
            persistedOrder.ShippingAddress.City.Should().Be("Taipei");
            persistedOrder.ShippingAddress.AddressLine1.Should().Be("123 Test St");
            persistedOrder.ShippingAddress.AddressLine2.Should().Be("Floor 4");

            var persistedInventory = await verifyAfterContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);

            persistedInventory.AvailableQuantity.Value.Should().Be(initialStock - orderQuantity); // 48
            persistedInventory.ReservedQuantity.Value.Should().Be(orderQuantity); // 2
            persistedInventory.Reservations.Should().ContainSingle(r =>
                r.OrderReference.Value == order.Id.Value &&
                r.Quantity.Value == orderQuantity);
        }

        // Act & Assert 2: Verify submitted order no longer appears as active Pending Cart
        var cartResponse = await client.GetAsync("/api/v1/cart");
        cartResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cartData = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();
        cartData.Should().NotBeNull();
        cartData!.Id.Should().BeNull();
        cartData.Items.Should().BeEmpty();

        // Act & Assert 3: Verify submitted order remains retrievable by the owning Customer with shipping address
        var getOrderResponse = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");
        getOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var orderData = await getOrderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        orderData.Should().NotBeNull();
        orderData!.Id.Should().Be(order.Id.Value);
        orderData.Status.Should().Be("Submitted");
        orderData.TotalAmount.Should().Be(200m);
        orderData.Items.Should().HaveCount(1);
        orderData.Items.First().ProductId.Should().Be(productId);
        orderData.Items.First().Quantity.Should().Be(orderQuantity);

        orderData.ShippingAddress.Should().NotBeNull();
        orderData.ShippingAddress!.RecipientName.Should().Be("Test Recipient");
        orderData.ShippingAddress.Phone.Should().Be("0912345678");
        orderData.ShippingAddress.CountryCode.Should().Be("TW");
        orderData.ShippingAddress.PostalCode.Should().Be("100");
        orderData.ShippingAddress.City.Should().Be("Taipei");
        orderData.ShippingAddress.AddressLine1.Should().Be("123 Test St");
        orderData.ShippingAddress.AddressLine2.Should().Be("Floor 4");
    }

    [Fact]
    public async Task SubmitOrder_RealMySql_MultiItemPartialFailure_RollsBackAll_LeavesOrderPending_AndNoPartialReservation()
    {
        // Arrange
        var customerId = Guid.NewGuid();

        // Deterministic sorting to guarantee productA is processed first by the handler
        var sortedGuids = new[] { Guid.NewGuid(), Guid.NewGuid() }.OrderBy(x => x).ToArray();
        var productAId = sortedGuids[0];
        var productBId = sortedGuids[1];

        var productARef = new ProductReference(productAId);
        var productBRef = new ProductReference(productBId);

        int productAInitialAvailable = 20;
        int productAOrderQuantity = 2; // Sufficient: 20 >= 2

        int productBInitialAvailable = 1;
        int productBOrderQuantity = 5; // Insufficient: 1 < 5

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(productAId), new Money(100m, "USD"), productAOrderQuantity);
        order.AddItem(new ProductId(productBId), new Money(200m, "USD"), productBOrderQuantity);

        var inventoryA = InventoryItem.Create(productARef);
        inventoryA.IncreaseStock(new StockQuantity(productAInitialAvailable));

        var inventoryB = InventoryItem.Create(productBRef);
        inventoryB.IncreaseStock(new StockQuantity(productBInitialAvailable));

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.InventoryItems.AddRange(inventoryA, inventoryB);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        // Mechanically verify state BEFORE submission
        await using (var verifyBefore = CreateFreshDbContext())
        {
            var orderBefore = await verifyBefore.Orders.SingleAsync(o => o.Id == order.Id);
            orderBefore.Status.Should().Be(OrderStatus.Pending);

            var invABefore = await verifyBefore.InventoryItems.Include(i => i.Reservations).SingleAsync(i => i.ProductReference == productARef);
            invABefore.AvailableQuantity.Value.Should().Be(productAInitialAvailable);
            invABefore.ReservedQuantity.Value.Should().Be(0);
            invABefore.Reservations.Should().BeEmpty();

            var invBBefore = await verifyBefore.InventoryItems.Include(i => i.Reservations).SingleAsync(i => i.ProductReference == productBRef);
            invBBefore.AvailableQuantity.Value.Should().Be(productBInitialAvailable);
            invBBefore.ReservedQuantity.Value.Should().Be(0);
            invBBefore.Reservations.Should().BeEmpty();
        }

        // Act: Execute submit via real HTTP endpoint
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = CreateValidSubmitOrderRequest();
        var response = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);

        // Assert: HTTP 400 BadRequest expected due to InsufficientStock
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert: Using a FRESH DbContext, verify full transaction rollback in real MySQL
        await using (var verifyAfter = CreateFreshDbContext())
        {
            var persistedOrder = await verifyAfter.Orders.SingleAsync(o => o.Id == order.Id);
            persistedOrder.Status.Should().Be(OrderStatus.Pending); // Still Pending!
            persistedOrder.ShippingAddress.Should().BeNull(); // No shipping snapshot persisted!

            var persistedA = await verifyAfter.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productARef);

            persistedA.AvailableQuantity.Value.Should().Be(productAInitialAvailable); // Exactly 20, NOT 18!
            persistedA.ReservedQuantity.Value.Should().Be(0); // Exactly 0, NOT 2!
            persistedA.Reservations.Should().BeEmpty(); // No partial reservation persisted!

            var persistedB = await verifyAfter.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productBRef);

            persistedB.AvailableQuantity.Value.Should().Be(productBInitialAvailable); // Exactly 1
            persistedB.ReservedQuantity.Value.Should().Be(0);
            persistedB.Reservations.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SubmitOrder_RealMySql_CrossCustomer_ReturnsNotFound_AndLeavesOrderUntouched()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var productRef = new ProductReference(productId);

        var order = Order.Create(customerA, "USD");
        order.AddItem(new ProductId(productId), new Money(100m, "USD"), 1);

        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(10));

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.InventoryItems.Add(inventoryItem);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        // Act: Customer B attempts to submit Customer A's order
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        var request = CreateValidSubmitOrderRequest();
        var response = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert: Customer A's order remains Pending and inventory untouched
        await using (var verifyContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyContext.Orders.SingleAsync(o => o.Id == order.Id);
            persistedOrder.Status.Should().Be(OrderStatus.Pending);
            persistedOrder.ShippingAddress.Should().BeNull();

            var persistedInventory = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);

            persistedInventory.AvailableQuantity.Value.Should().Be(10);
            persistedInventory.ReservedQuantity.Value.Should().Be(0);
            persistedInventory.Reservations.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SubmitOrder_RealMySql_MissingOrInvalidAddress_Returns400_AndLeavesOrderPending()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var productRef = new ProductReference(productId);

        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(productId), new Money(100m, "USD"), 1);

        var inventoryItem = InventoryItem.Create(productRef);
        inventoryItem.IncreaseStock(new StockQuantity(10));

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.InventoryItems.Add(inventoryItem);
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        // Case 1: Missing Address (null payload)
        var responseNull = await client.PutAsJsonAsync<SubmitOrderRequest?>($"/api/v1/orders/{order.Id.Value}/submit", null);
        responseNull.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Case 2: Invalid CountryCode in Address
        var invalidRequest = new SubmitOrderRequest(new ShippingAddressRequest(
            "Test",
            "0912345678",
            "INVALID_CODE",
            "100",
            "Taipei",
            "123 St",
            null));
        var responseInvalid = await client.PutAsJsonAsync($"/api/v1/orders/{order.Id.Value}/submit", invalidRequest);
        responseInvalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert: Order remains Pending and inventory untouched
        await using (var verifyContext = CreateFreshDbContext())
        {
            var persistedOrder = await verifyContext.Orders.SingleAsync(o => o.Id == order.Id);
            persistedOrder.Status.Should().Be(OrderStatus.Pending);
            persistedOrder.ShippingAddress.Should().BeNull();

            var persistedInventory = await verifyContext.InventoryItems
                .Include(i => i.Reservations)
                .SingleAsync(i => i.ProductReference == productRef);

            persistedInventory.AvailableQuantity.Value.Should().Be(10);
            persistedInventory.ReservedQuantity.Value.Should().Be(0);
            persistedInventory.Reservations.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task SubmitOrder_RealMySql_HistoricalRowWithNullShippingAddress_RemainsQueryable()
    {
        // Arrange: Insert a pre-existing order directly into DB with null ShippingAddress
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = Order.Create(customerId, "USD");
        order.AddItem(new ProductId(productId), new Money(100m, "USD"), 1);
        // Direct status change without shipping address to simulate historical migrated row
        order.ChangeStatus(OrderStatus.Submitted);

        await using (var dbContext = CreateFreshDbContext())
        {
            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();
        }

        // Verify via fresh DbContext
        await using (var freshContext = CreateFreshDbContext())
        {
            var persistedHistorical = await freshContext.Orders.SingleAsync(o => o.Id == order.Id);
            persistedHistorical.Status.Should().Be(OrderStatus.Submitted);
            persistedHistorical.ShippingAddress.Should().BeNull();
        }

        // Act: Query through WebApi endpoint
        using var client = _factory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.GetAsync($"/api/v1/orders/{order.Id.Value}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var data = await response.Content.ReadFromJsonAsync<OrderResponse>();
        data.Should().NotBeNull();
        data!.Id.Should().Be(order.Id.Value);
        data.Status.Should().Be("Submitted");
        data.ShippingAddress.Should().BeNull(); // Neutral null for historical order
    }
}
