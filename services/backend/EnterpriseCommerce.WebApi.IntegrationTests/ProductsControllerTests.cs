using System.Net;
using System.Net.Http.Json;
using EnterpriseCommerce.Application.Catalog.Commands.CreateProduct;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductById;
using EnterpriseCommerce.Application.Catalog.Queries.GetProductBySku;
using EnterpriseCommerce.Application.Catalog.Queries.GetProducts;
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
        var request = new CreateProductRequest("Integration Test Product", "SKU-INT-1", 100m, "TWD");
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
    public async Task CreateProduct_AsCustomer_ReturnsForbidden()
    {
        // Arrange
        var request = new CreateProductRequest("Integration Test Product", "SKU-INT-2", 100m, "TWD");
        
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
}
