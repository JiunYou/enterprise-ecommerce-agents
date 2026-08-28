using EnterpriseCommerce.Application.Orders.Commands.CreateOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class OrdersControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public OrdersControllerTests(WebApplicationFactory<Program> factory)
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
                
                // Replace ISender with our mock
                services.AddSingleton(_senderMock.Object);
            });
        });
    }

    [Fact]
    public async Task Post_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new CreateOrderRequest(Guid.NewGuid(), "USD");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Orders", request);

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, content);
    }

    [Fact]
    public async Task Post_WithValidPayload_Returns201Created()
    {
        // Arrange
        var expectedOrderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedOrderId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var request = new CreateOrderRequest(Guid.NewGuid(), "USD");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(expectedOrderId.ToString());
    }

    [Fact]
    public async Task Post_WithEmptyCurrency_Returns400BadRequest_AndRFC7807ProblemDetails()
    {
        // Arrange
        var error = new Error("Order.Currency", "Currency is required.");
        _senderMock.Setup(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(error));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var request = new CreateOrderRequest(Guid.NewGuid(), "");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Orders", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("Currency is required.");
    }

    [Fact]
    public async Task Get_HealthCheck_Returns200OK()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/v1/Orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithExistingOrder_Returns200OK_WithOrderDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderResponse = new EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderResponse(
            orderId,
            customerId,
            "Pending",
            "TWD",
            500m,
            new List<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderItemResponse>
            {
                new(Guid.NewGuid(), 250m, "TWD", 2, 500m)
            });

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.GetOrderByIdQuery>(q => q.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(orderResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.GetAsync($"/api/v1/Orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(orderId);
        content.CustomerId.Should().Be(customerId);
        content.TotalAmount.Should().Be(500m);
        content.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WithNonExistingOrder_Returns404NotFound_AndProblemDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.GetOrderByIdQuery>(q => q.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderResponse>(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.GetAsync($"/api/v1/Orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task AddOrderItem_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var request = new AddOrderItemRequest(Guid.NewGuid(), 2);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/Orders/{orderId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddOrderItem_WithValidPayload_Returns200OK()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new AddOrderItemRequest(productId, 2);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand>(c =>
            c.OrderId == orderId && c.ProductId == productId && c.Quantity == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/Orders/{orderId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddOrderItem_WithNonExistingOrder_Returns404NotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new AddOrderItemRequest(Guid.NewGuid(), 2);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/Orders/{orderId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task AddOrderItem_WithCurrencyMismatch_Returns400BadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var request = new AddOrderItemRequest(Guid.NewGuid(), 2);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.CurrencyMismatch));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/Orders/{orderId}/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("Item currency must match order currency.");
    }

    [Fact]
    public async Task CancelOrder_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelOrder_WithExistingOrder_Returns200OK()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CancelOrder_WithNonExistingOrder_Returns404NotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task CancelOrder_WithInvalidStatusTransition_Returns400BadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.InvalidStatusTransition));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/cancel", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("The status transition is not allowed.");
    }

    [Fact]
    public async Task RemoveOrderItem_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/v1/Orders/{orderId}/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveOrderItem_WithValidPayload_Returns200OK()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem.RemoveOrderItemCommand>(c =>
            c.OrderId == orderId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.DeleteAsync($"/api/v1/Orders/{orderId}/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveOrderItem_WithNonExistingOrder_Returns404NotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem.RemoveOrderItemCommand>(c =>
            c.OrderId == orderId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        // Act
        var response = await client.DeleteAsync($"/api/v1/Orders/{orderId}/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task PayOrder_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/pay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PayOrder_WithExistingOrder_Returns200OK()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid.MarkOrderAsPaidCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/pay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PayOrder_WithEmptyOrder_Returns400BadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.MarkOrderAsPaid.MarkOrderAsPaidCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.EmptyOrder));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/pay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("Cannot perform operation on an empty order.");
    }

    [Fact]
    public async Task ShipOrder_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/ship", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ShipOrder_WithPaidOrder_Returns200OK()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.ShipOrder.ShipOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/ship", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ShipOrder_WithPendingOrder_Returns400BadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.ShipOrder.ShipOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.InvalidStatusTransition));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/ship", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("The status transition is not allowed.");
    }

    [Fact]
    public async Task PayOrder_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/pay", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ShipOrder_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/ship", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
