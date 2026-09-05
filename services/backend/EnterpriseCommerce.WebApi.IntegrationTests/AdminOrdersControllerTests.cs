using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrderById;
using EnterpriseCommerce.Application.Orders.Queries.GetAdminOrders;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class AdminOrdersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public AdminOrdersControllerTests(WebApplicationFactory<Program> factory)
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

                services.AddSingleton(_senderMock.Object);
            });
        });
    }

    private HttpClient CreateClientWithRole(string? role = null, Guid? customerId = null)
    {
        var client = _factory.CreateClient();
        if (role != null || customerId != null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
            if (role != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Role", role);
            }
            if (customerId != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());
            }
        }
        return client;
    }

    // ==========================================
    // LIST ENDPOINT: GET /api/v1/admin/orders
    // ==========================================

    [Fact]
    public async Task GetOrders_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<GetAdminOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrders_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var client = CreateClientWithRole("Customer", Guid.NewGuid());

        // Act
        var response = await client.GetAsync("/api/v1/admin/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("ShippingAddress");
        _senderMock.Verify(m => m.Send(It.IsAny<GetAdminOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrders_WithCustomerIdentityClaimAlone_Returns403Forbidden()
    {
        // Arrange (No X-Test-Role header, only CustomerId claim)
        var client = CreateClientWithRole(null, Guid.NewGuid());

        // Act
        var response = await client.GetAsync("/api/v1/admin/orders");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<GetAdminOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrders_WithAdminToken_Returns200OK()
    {
        // Arrange
        var sampleSummary = new AdminOrderSummaryResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Paid",
            "TWD",
            300m,
            DateTimeOffset.UtcNow);

        var pageResponse = new AdminOrderPageResponse(
            new List<AdminOrderSummaryResponse> { sampleSummary },
            Page: 1,
            PageSize: 25,
            TotalCount: 1);

        _senderMock.Setup(m => m.Send(It.IsAny<GetAdminOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(pageResponse));

        var client = CreateClientWithRole("Admin");

        // Act
        var response = await client.GetAsync("/api/v1/admin/orders?page=1&pageSize=25");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<AdminOrderPageResponse>();
        content.Should().NotBeNull();
        content!.TotalCount.Should().Be(1);
        content.Items.Should().HaveCount(1);
        content.Items[0].Id.Should().Be(sampleSummary.Id);

        // Verify no address or order item details (PII / item specifics) in list response body
        var rawBody = await response.Content.ReadAsStringAsync();
        rawBody.Should().NotContain("shippingAddress", because: "list endpoint must not return shipping address");
        rawBody.Should().NotContain("recipientName");
        rawBody.Should().NotContain("addressLine1");
        rawBody.Should().NotContain("productId", because: "list endpoint must not return order items detail");
        rawBody.Should().NotContain("unitPrice");
        rawBody.Should().NotContain("quantity");
    }

    [Fact]
    public async Task GetOrders_WithInvalidStatus_Returns400BadRequest()
    {
        // Arrange
        _senderMock.Setup(m => m.Send(It.Is<GetAdminOrdersQuery>(q => q.Status == "InvalidStatus"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AdminOrderPageResponse>(
                new Error("AdminOrders.InvalidStatus", "Invalid order status 'InvalidStatus'.")));

        var client = CreateClientWithRole("Admin");

        // Act
        var response = await client.GetAsync("/api/v1/admin/orders?status=InvalidStatus");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("InvalidStatus");
    }

    // ==========================================
    // DETAIL ENDPOINT: GET /api/v1/admin/orders/{id}
    // ==========================================

    [Fact]
    public async Task GetOrderById_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/admin/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<GetAdminOrderByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrderById_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var client = CreateClientWithRole("Customer", Guid.NewGuid());
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/admin/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("ShippingAddress");
        body.Should().NotContain("RecipientName");
        _senderMock.Verify(m => m.Send(It.IsAny<GetAdminOrderByIdQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrderById_WithAdminToken_ExistingOrder_Returns200OK()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var detailResponse = new AdminOrderDetailResponse(
            orderId,
            customerId,
            "Shipped",
            "TWD",
            500m,
            DateTimeOffset.UtcNow,
            new List<OrderItemResponse>
            {
                new(Guid.NewGuid(), 250m, "TWD", 2, 500m)
            },
            new ShippingAddressResponse(
                "Recipient", "+886912345678", "TW", "100", "Taipei", "Line 1", null));

        _senderMock.Setup(m => m.Send(It.Is<GetAdminOrderByIdQuery>(q => q.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(detailResponse));

        var client = CreateClientWithRole("Admin");

        // Act
        var response = await client.GetAsync($"/api/v1/admin/orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<AdminOrderDetailResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(orderId);
        content.Status.Should().Be("Shipped");
        content.SubmittedAt.Should().NotBeNull();
        content.Items.Should().HaveCount(1);
        content.ShippingAddress.Should().NotBeNull();
        content.ShippingAddress!.RecipientName.Should().Be("Recipient");
    }

    [Fact]
    public async Task GetOrderById_WithAdminToken_MissingOrder_Returns404NotFound()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<GetAdminOrderByIdQuery>(q => q.OrderId == missingId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AdminOrderDetailResponse>(OrderErrors.NotFound));

        var client = CreateClientWithRole("Admin");

        // Act
        var response = await client.GetAsync($"/api/v1/admin/orders/{missingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ==========================================
    // CUSTOMER ENDPOINT REGRESSION CHECK
    // ==========================================

    [Fact]
    public async Task CustomerGetOrderById_PreservesCustomerOwnership()
    {
        // Arrange
        var requestingCustomerId = Guid.NewGuid();
        var targetOrderId = Guid.NewGuid();

        // Handler returns NotFound when order does not belong to requesting customer
        _senderMock.Setup(m => m.Send(
                It.Is<GetOrderByIdQuery>(q => q.OrderId == targetOrderId && q.CustomerId == requestingCustomerId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OrderResponse>(OrderErrors.NotFound));

        var client = CreateClientWithRole("Customer", requestingCustomerId);

        // Act
        var response = await client.GetAsync($"/api/v1/Orders/{targetOrderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        _senderMock.Verify(m => m.Send(
            It.Is<GetOrderByIdQuery>(q => q.OrderId == targetOrderId && q.CustomerId == requestingCustomerId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ==========================================
    // ROUTE VERIFICATION: EXACT ROUTE AND NO UNINTENDED ALIAS
    // ==========================================

    [Fact]
    public async Task AdminRoutes_AreRoutable_AndNoUnintendedAlias()
    {
        // Arrange
        var client = CreateClientWithRole("Admin");
        var orderId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(It.IsAny<GetAdminOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AdminOrderPageResponse(new List<AdminOrderSummaryResponse>(), 1, 25, 0)));

        _senderMock.Setup(m => m.Send(It.IsAny<GetAdminOrderByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AdminOrderDetailResponse>(OrderErrors.NotFound));

        // Act 1: GET /api/v1/admin/orders is routable
        var listResponse = await client.GetAsync("/api/v1/admin/orders");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act 2: GET /api/v1/admin/orders/{id} is routable
        var detailResponse = await client.GetAsync($"/api/v1/admin/orders/{orderId}");
        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound); // Hit the controller, got NotFound from mock

        // Act 3: Verify unintended route alias /api/v1/AdminOrders does NOT exist
        var unintendedAliasResponse = await client.GetAsync("/api/v1/AdminOrders");
        unintendedAliasResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var unintendedDetailAliasResponse = await client.GetAsync($"/api/v1/AdminOrders/{orderId}");
        unintendedDetailAliasResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
