using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Catalog.Queries.GetProducts;
using EnterpriseCommerce.Application.Common.Models;
using EnterpriseCommerce.Application.Inventory.Commands.DecreaseInventoryStock;
using EnterpriseCommerce.Application.Inventory.Commands.IncreaseInventoryStock;
using EnterpriseCommerce.Application.Inventory.Queries.GetInventoryByProductId;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Inventory;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Inventory;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class AdminProductsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public AdminProductsControllerTests(WebApplicationFactory<Program> factory)
    {
        _senderMock = new Mock<ISender>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", "Server=localhost;Database=Test;Uid=test;Pwd=test;");

            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.DefaultScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.DefaultScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, options => { });

                var senderDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISender));
                if (senderDescriptor != null)
                {
                    services.Remove(senderDescriptor);
                }
                services.AddTransient(_ => _senderMock.Object);
            });
        });
    }

    [Fact]
    public async Task GetProducts_AnonymousUser_ReturnsUnauthorized401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/products");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProducts_NonAdminUser_ReturnsForbidden403()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/products");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProducts_AdminUser_ReturnsOk200_WithPagedList()
    {
        // Arrange
        var pagedList = PagedList<ProductResponse>.Create(
            new List<ProductResponse>
            {
                new(Guid.NewGuid(), "Admin Test Item", "SKU-ADMIN-1", 99m, "TWD", true)
            },
            page: 1,
            pageSize: 10,
            totalCount: 1);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductsQuery>(q => q.Page == 1 && q.PageSize == 10 && q.OnlyActive == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedList));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/products?page=1&pageSize=10");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<PagedList<ProductResponse>>();
        content.Should().NotBeNull();
        content!.Items.Should().HaveCount(1);
        content.Items[0].Sku.Should().Be("SKU-ADMIN-1");
    }

    [Fact]
    public async Task GetProducts_AdminUser_ForwardsOnlyActiveFalseDirectly()
    {
        // Arrange
        var pagedList = PagedList<ProductResponse>.Create(
            new List<ProductResponse>
            {
                new(Guid.NewGuid(), "Inactive Product", "SKU-INACTIVE-1", 50m, "TWD", false)
            },
            page: 1,
            pageSize: 10,
            totalCount: 1);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductsQuery>(q => q.OnlyActive == false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedList));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/products?onlyActive=false");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert: 驗證 Admin 端點將 onlyActive=false 忠實轉發給 GetProductsQuery
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _senderMock.Verify(m => m.Send(
            It.Is<GetProductsQuery>(q => q.OnlyActive == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_AdminUser_InvalidParameters_ReturnsBadRequest400()
    {
        // Arrange
        _senderMock.Setup(m => m.Send(
                It.IsAny<GetProductsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagedList<ProductResponse>>(new Error("Validation.Error", "每頁數量不能超過 100。")));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/products?pageSize=200");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAdminProductById_AnonymousUser_ReturnsUnauthorized401()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/admin/products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAdminProductById_NonAdminUser_ReturnsForbidden403()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/products/{productId}");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminProductById_AdminUser_AllowsInactiveProduct()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productResponse = new ProductResponse(productId, "Inactive Product", "SKU-INACT", 200m, "TWD", false);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductByIdQuery>(q => q.ProductId == productId && q.AllowInactive == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(productResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/products/{productId}");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ProductResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(productId);
        content.IsActive.Should().BeFalse();
        _senderMock.Verify(m => m.Send(
            It.Is<GetProductByIdQuery>(q => q.ProductId == productId && q.AllowInactive == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAdminProductById_AdminUser_NotFound_Returns404()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(
                It.IsAny<GetProductByIdQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ProductResponse>(ProductErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/products/{productId}");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #region Inventory Tests

    [Fact]
    public async Task GetInventory_AnonymousUser_ReturnsUnauthorized401()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/admin/products/{productId}/inventory");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInventory_NonAdminUser_ReturnsForbidden403()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/products/{productId}/inventory");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetInventory_AdminUser_ExistingInventory_ReturnsOk200()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var inventoryResponse = new InventoryResponse(productId, 10, 2);

        _senderMock.Setup(m => m.Send(
                It.Is<GetInventoryByProductIdQuery>(q => q.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(inventoryResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/products/{productId}/inventory");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<AdminInventoryResponse>();
        content.Should().NotBeNull();
        content!.ProductId.Should().Be(productId);
        content.AvailableQuantity.Should().Be(10);
        content.ReservedQuantity.Should().Be(2);
    }

    [Fact]
    public async Task GetInventory_AdminUser_MissingInventory_ReturnsNotFound404()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<GetInventoryByProductIdQuery>(q => q.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<InventoryResponse>(InventoryErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/products/{productId}/inventory");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IncreaseInventory_AnonymousUser_ReturnsUnauthorized401()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/admin/products/{productId}/inventory/increase", new AdjustInventoryStockRequest(5));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IncreaseInventory_NonAdminUser_ReturnsForbidden403()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/increase");
        requestMessage.Headers.Add("X-Test-Role", "Customer");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IncreaseInventory_AdminUser_ValidQuantity_ReturnsOk200()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<IncreaseInventoryStockCommand>(c => c.ProductId == productId && c.Quantity == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/increase");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task IncreaseInventory_AdminUser_InvalidQuantity_ReturnsBadRequest400()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<IncreaseInventoryStockCommand>(c => c.ProductId == productId && c.Quantity <= 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(InventoryErrors.NegativeQuantity));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/increase");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(0));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IncreaseInventory_AdminUser_MissingInventory_ReturnsNotFound404()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<IncreaseInventoryStockCommand>(c => c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(InventoryErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/increase");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IncreaseInventory_AdminUser_ConcurrencyConflict_ReturnsConflict409()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<IncreaseInventoryStockCommand>(c => c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(InventoryErrors.ConcurrencyConflict));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/increase");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DecreaseInventory_AnonymousUser_ReturnsUnauthorized401()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/admin/products/{productId}/inventory/decrease", new AdjustInventoryStockRequest(5));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DecreaseInventory_NonAdminUser_ReturnsForbidden403()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/decrease");
        requestMessage.Headers.Add("X-Test-Role", "Customer");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DecreaseInventory_AdminUser_ValidQuantity_ReturnsOk200()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<DecreaseInventoryStockCommand>(c => c.ProductId == productId && c.Quantity == 4),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/decrease");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(4));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DecreaseInventory_AdminUser_InsufficientStock_ReturnsBadRequest400()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<DecreaseInventoryStockCommand>(c => c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(InventoryErrors.InsufficientStock));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/decrease");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(999));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DecreaseInventory_AdminUser_MissingInventory_ReturnsNotFound404()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<DecreaseInventoryStockCommand>(c => c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(InventoryErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/decrease");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DecreaseInventory_AdminUser_ConcurrencyConflict_ReturnsConflict409()
    {
        // Arrange
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<DecreaseInventoryStockCommand>(c => c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(InventoryErrors.ConcurrencyConflict));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/inventory/decrease");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(new AdjustInventoryStockRequest(5));

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion
}
