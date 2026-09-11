using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;
using EnterpriseCommerce.Application.Catalog.Commands.DeactivateProduct;
using EnterpriseCommerce.Application.Catalog.Commands.ReactivateProduct;
using EnterpriseCommerce.Application.Catalog.Commands.UpdateProductPrice;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductBySku;
using EnterpriseCommerce.Application.Catalog.Queries.GetProducts;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Catalog;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class ProductsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public ProductsControllerTests(WebApplicationFactory<Program> factory)
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
    public async Task CreateProduct_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new CreateProductRequest("Integration Test Product", "SKU-INT-1", 100m, "TWD", 10);
        var productId = Guid.NewGuid();
        
        _senderMock.Setup(m => m.Send(It.IsAny<CreateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(productId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Products");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateProduct_Anonymous_ReturnsUnauthorized()
    {
        // Arrange
        var request = new CreateProductRequest("Integration Test Product", "SKU-INT-ANON", 100m, "TWD", 10);
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateProduct_AsCustomer_ReturnsForbidden()
    {
        // Arrange
        var request = new CreateProductRequest("Integration Test Product", "SKU-INT-2", 100m, "TWD", 10);
        
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Products");
        requestMessage.Headers.Add("X-Test-Role", "Customer");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProduct_WithNonPositiveInitialStock_ReturnsBadRequest()
    {
        // Arrange (InitialStock = 0, should fail validation and return 400)
        var request = new CreateProductRequest("Integration Test Product", "SKU-INT-3", 100m, "TWD", 0);

        // When validation fails in MediatR pipeline, ValidationException is thrown which GlobalExceptionHandler maps to 400
        _senderMock.Setup(m => m.Send(It.Is<CreateProductCommand>(c => c.InitialStock <= 0), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EnterpriseCommerce.Application.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(CreateProductCommand.InitialStock), "'Initial Stock' must be greater than '0'.")
            }));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/v1/Products");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProducts_AnonymousUser_ForcesOnlyActiveTrue()
    {
        // Arrange
        var pagedList = EnterpriseCommerce.Application.Common.Models.PagedList<ProductResponse>.Create(
            new List<ProductResponse>(),
            page: 1,
            pageSize: 10,
            totalCount: 0);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductsQuery>(q => q.OnlyActive == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedList));

        var client = _factory.CreateClient();

        // Act: 匿名使用者企圖帶入 onlyActive=false
        var response = await client.GetAsync("/api/v1/Products?page=1&pageSize=10&onlyActive=false");

        // Assert: 應成功呼叫且傳給 Query 的 OnlyActive 應被安全防禦強制轉為 true
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _senderMock.Verify(m => m.Send(
            It.Is<GetProductsQuery>(q => q.OnlyActive == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProducts_AdminUser_AllowsOnlyActiveFalse()
    {
        // Arrange
        var pagedList = EnterpriseCommerce.Application.Common.Models.PagedList<ProductResponse>.Create(
            new List<ProductResponse>(),
            page: 1,
            pageSize: 10,
            totalCount: 0);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductsQuery>(q => q.OnlyActive == false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedList));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/Products?page=1&pageSize=10&onlyActive=false");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert: Admin 應被允許查詢 onlyActive=false
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _senderMock.Verify(m => m.Send(
            It.Is<GetProductsQuery>(q => q.OnlyActive == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductById_AnonymousUser_ReturnsOk()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var productResponse = new ProductResponse(productId, "Single Product", "SKU-SINGLE-1", 150m, "TWD", true);

        _senderMock.Setup(m => m.Send(It.Is<GetProductByIdQuery>(q => q.ProductId == productId && !q.AllowInactive), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(productResponse));

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/Products/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ProductResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(productId);
    }

    [Fact]
    public async Task GetProductBySku_AnonymousUser_ReturnsOk()
    {
        // Arrange
        var sku = "SKU-TEST-123";
        var productResponse = new ProductResponse(Guid.NewGuid(), "Sku Product", sku, 200m, "TWD", true);

        _senderMock.Setup(m => m.Send(It.Is<GetProductBySkuQuery>(q => q.Sku == sku && !q.AllowInactive), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(productResponse));

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/v1/Products/sku/{sku}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ProductResponse>();
        content.Should().NotBeNull();
        content!.Sku.Should().Be(sku);
    }

    [Fact]
    public async Task GetProducts_NonAdminUser_ForcesOnlyActiveTrue()
    {
        // Arrange
        var pagedList = EnterpriseCommerce.Application.Common.Models.PagedList<ProductResponse>.Create(
            new List<ProductResponse>(),
            page: 1,
            pageSize: 10,
            totalCount: 0);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductsQuery>(q => q.OnlyActive == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pagedList));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/Products?page=1&pageSize=10&onlyActive=false");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act: 非 Admin 顧客即使帶入 onlyActive=false，後端防禦仍強制轉為 true
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _senderMock.Verify(m => m.Send(
            It.Is<GetProductsQuery>(q => q.OnlyActive == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductById_NonAdminUser_WhenInactive_ReturnsNotFound()
    {
        // Arrange: 模擬非活躍商品對非 Admin 查詢回傳 NotFound 失敗
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(
                It.Is<GetProductByIdQuery>(q => q.ProductId == productId && !q.AllowInactive),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ProductResponse>(ProductErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/Products/{productId}");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateProductPrice_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        var request = new UpdateProductPriceRequest(120m);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/Products/{productId}/price", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProductPrice_NonAdminUser_ReturnsForbidden()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var request = new UpdateProductPriceRequest(120m);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/price");
        requestMessage.Headers.Add("X-Test-Role", "Customer");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateProductPrice_AdminUser_Success_ReturnsOk()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<UpdateProductPriceCommand>(c => c.ProductId == productId && c.NewPrice == 150m), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var request = new UpdateProductPriceRequest(150m);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/price");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateProductPrice_InvalidPrice_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<UpdateProductPriceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.InvalidPrice));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var request = new UpdateProductPriceRequest(-10m);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/price");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProductPrice_NotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<UpdateProductPriceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var request = new UpdateProductPriceRequest(100m);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/price");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateProductPrice_ConcurrencyConflict_ReturnsConflict409()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<UpdateProductPriceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.ConcurrencyConflict));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var request = new UpdateProductPriceRequest(120m);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/price");
        requestMessage.Headers.Add("X-Test-Role", "Admin");
        requestMessage.Content = JsonContent.Create(request);

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeactivateProduct_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/api/v1/Products/{productId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeactivateProduct_NonAdminUser_ReturnsForbidden()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/deactivate");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeactivateProduct_AdminUser_Success_ReturnsOk()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<DeactivateProductCommand>(c => c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/deactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeactivateProduct_AlreadyDeactivated_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<DeactivateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.AlreadyDeactivated));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/deactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeactivateProduct_NotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<DeactivateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/deactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateProduct_ConcurrencyConflict_ReturnsConflict409()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<DeactivateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.ConcurrencyConflict));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/deactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ReactivateProduct_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsync($"/api/v1/Products/{productId}/reactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ReactivateProduct_NonAdminUser_ReturnsForbidden()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/reactivate");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReactivateProduct_AdminUser_Success_ReturnsOk()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<ReactivateProductCommand>(c => c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/reactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReactivateProduct_AlreadyActive_ReturnsBadRequest()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<ReactivateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.AlreadyActive));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/reactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReactivateProduct_NotFound_ReturnsNotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<ReactivateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/reactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReactivateProduct_ConcurrencyConflict_ReturnsConflict409()
    {
        // Arrange
        var productId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<ReactivateProductCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.ConcurrencyConflict));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/Products/{productId}/reactivate");
        requestMessage.Headers.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetProductById_AfterReactivation_BecomesVisibleToNonAdmin()
    {
        // Arrange: 模擬商品經由 Reactivate 成為 Active 後，非 Admin 查詢得以成功取得
        var productId = Guid.NewGuid();
        var reactivatedProductResponse = new ProductResponse(productId, "Reactivated Item", "SKU-REACTIVATED-1", 100m, "TWD", true);

        _senderMock.Setup(m => m.Send(
                It.Is<GetProductByIdQuery>(q => q.ProductId == productId && !q.AllowInactive),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(reactivatedProductResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/Products/{productId}");
        requestMessage.Headers.Add("X-Test-Role", "Customer");

        // Act
        var response = await client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ProductResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(productId);
        content.IsActive.Should().BeTrue();
    }
}
