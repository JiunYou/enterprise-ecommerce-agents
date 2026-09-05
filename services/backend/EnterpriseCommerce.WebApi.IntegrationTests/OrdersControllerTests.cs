using EnterpriseCommerce.Application.Orders.Commands.CreateOrder;
using EnterpriseCommerce.Application.Orders.Queries.GetFulfillmentOrders;
using EnterpriseCommerce.Application.Orders.Queries.GetOrderById;
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
        var request = new CreateOrderRequest("USD");

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
        var customerId = Guid.NewGuid();
        var expectedOrderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<CreateOrderCommand>(c => c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedOrderId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new CreateOrderRequest("USD");

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
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        var request = new CreateOrderRequest("");

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

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.GetOrderByIdQuery>(q => q.OrderId == orderId && q.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(orderResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
    public async Task GetById_WithShippingAddress_Returns200OK_WithShippingDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var shippingResponse = new EnterpriseCommerce.Application.Orders.Queries.GetOrderById.ShippingAddressResponse(
            "Jane Doe",
            "0912345678",
            "TW",
            "100",
            "Taipei",
            "123 Main St",
            "Apt 4B");

        var orderResponse = new EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderResponse(
            orderId,
            customerId,
            "Submitted",
            "TWD",
            500m,
            new List<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderItemResponse>
            {
                new(Guid.NewGuid(), 250m, "TWD", 2, 500m)
            },
            shippingResponse);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.GetOrderByIdQuery>(q => q.OrderId == orderId && q.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(orderResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        // Act
        var response = await client.GetAsync($"/api/v1/Orders/{orderId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderResponse>();
        content.Should().NotBeNull();
        content!.ShippingAddress.Should().NotBeNull();
        content.ShippingAddress!.RecipientName.Should().Be("Jane Doe");
        content.ShippingAddress.Phone.Should().Be("0912345678");
        content.ShippingAddress.CountryCode.Should().Be("TW");
        content.ShippingAddress.PostalCode.Should().Be("100");
        content.ShippingAddress.City.Should().Be("Taipei");
        content.ShippingAddress.AddressLine1.Should().Be("123 Main St");
        content.ShippingAddress.AddressLine2.Should().Be("Apt 4B");
    }

    [Fact]
    public async Task GetById_WithNonExistingOrder_Returns404NotFound_AndProblemDetails()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.GetOrderByIdQuery>(q => q.OrderId == orderId && q.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<EnterpriseCommerce.Application.Orders.Queries.GetOrderById.OrderResponse>(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();
        var request = new AddOrderItemRequest(productId, 2);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand>(c =>
            c.OrderId == orderId && c.ProductId == productId && c.Quantity == 2 && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();
        var request = new AddOrderItemRequest(Guid.NewGuid(), 2);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();
        var request = new AddOrderItemRequest(Guid.NewGuid(), 2);

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.AddOrderItem.AddOrderItemCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.CurrencyMismatch));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.CancelOrder.CancelOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.InvalidStatusTransition));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem.RemoveOrderItemCommand>(c =>
            c.OrderId == orderId && c.ProductId == productId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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
        var customerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.RemoveOrderItem.RemoveOrderItemCommand>(c =>
            c.OrderId == orderId && c.ProductId == productId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

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

    [Fact]
    public async Task Post_WithoutXTestUserId_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var request = new CreateOrderRequest("USD");
        var response = await client.PostAsJsonAsync("/api/v1/Orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithInvalidXTestUserId_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "not-a-guid");

        var request = new CreateOrderRequest("USD");
        var response = await client.PostAsJsonAsync("/api/v1/Orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Post_WithGuidEmptyXTestUserId_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.Empty.ToString());

        var request = new CreateOrderRequest("USD");
        var response = await client.PostAsJsonAsync("/api/v1/Orders", request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<CreateOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task SubmitOrder_WithExistingOrder_Returns200OK()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidSubmitOrderRequest();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(c =>
            c.OrderId == orderId &&
            c.CustomerId == customerId &&
            c.ShippingAddress.RecipientName == request.ShippingAddress!.RecipientName), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitOrder_WhenMissingShippingAddress_Returns400BadRequest()
    {
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var requestWithNullAddress = new SubmitOrderRequest(null);
        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", requestWithNullAddress);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _senderMock.Verify(m => m.Send(It.IsAny<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrder_WhenInvalidShippingAddress_Returns400BadRequest()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidSubmitOrderRequest();
        _senderMock.Setup(m => m.Send(It.IsAny<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(EnterpriseCommerce.Domain.Orders.OrderErrors.InvalidShippingCountryCode));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitOrder_WithoutAuthToken_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/submit", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrder_WithoutCustomerIdClaim_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        // Do not add X-Test-User-Id header
        var orderId = Guid.NewGuid();

        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/submit", null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitOrder_WhenOrderNotFoundOrNotOwned_Returns404NotFound()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidSubmitOrderRequest();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Title.Should().Be("Not Found");
    }

    [Fact]
    public async Task SubmitOrder_WhenInsufficientStock_Returns400BadRequest()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidSubmitOrderRequest();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(EnterpriseCommerce.Domain.Inventory.InventoryErrors.InsufficientStock));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Detail.Should().Be(EnterpriseCommerce.Domain.Inventory.InventoryErrors.InsufficientStock.Message);
    }

    [Fact]
    public async Task SubmitOrder_WhenEmptyOrder_Returns400BadRequest()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidSubmitOrderRequest();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.EmptyOrder));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Detail.Should().Be(OrderErrors.EmptyOrder.Message);
    }

    [Fact]
    public async Task SubmitOrder_WhenAlreadySubmitted_Returns400BadRequest()
    {
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var request = CreateValidSubmitOrderRequest();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.SubmitOrder.SubmitOrderCommand>(c => c.OrderId == orderId && c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.InvalidStatusTransition));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var response = await client.PutAsJsonAsync($"/api/v1/Orders/{orderId}/submit", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Detail.Should().Be(OrderErrors.InvalidStatusTransition.Message);
    }

    [Fact]
    public async Task GetFulfillmentOrders_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<GetFulfillmentOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFulfillmentOrders_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("ShippingAddress");
        body.Should().NotContain("RecipientName");
        body.Should().NotContain("AddressLine1");
        _senderMock.Verify(m => m.Send(It.IsAny<GetFulfillmentOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFulfillmentOrders_WithCustomerIdentityClaimAlone_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<GetFulfillmentOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFulfillmentOrders_WithWrongRoleClaimType_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        client.DefaultRequestHeaders.Add("X-Test-Role-Claim-Type", "urn:custom:wrong_role_type");

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<GetFulfillmentOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Guest")]
    [InlineData("Manager")]
    public async Task GetFulfillmentOrders_WithNonAdminOrMalformedRole_Returns403Forbidden(string role)
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<GetFulfillmentOrdersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFulfillmentOrders_WithAdminToken_Returns200OKAndOnlyPaidOrders()
    {
        // Arrange
        var expectedOrders = new List<OrderResponse>
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Paid",
                "TWD",
                300m,
                new List<OrderItemResponse>
                {
                    new(Guid.NewGuid(), 150m, "TWD", 2, 300m)
                },
                new ShippingAddressResponse(
                    "Jane Doe",
                    "+886912345678",
                    "TW",
                    "100",
                    "Taipei",
                    "Main St",
                    "Floor 2"))
        };

        _senderMock.Setup(m => m.Send(It.Is<GetFulfillmentOrdersQuery>(q => q.Limit == 25), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<OrderResponse>>(expectedOrders));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment?limit=25");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.Should().NotBeNull();
        orders!.Should().HaveCount(1);
        orders[0].Status.Should().Be("Paid");
        orders[0].ShippingAddress.Should().NotBeNull();
        orders[0].ShippingAddress!.RecipientName.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetFulfillmentOrders_WithHistoricalPaidOrderHavingNullShippingAddress_Returns200OKWithNullAddress()
    {
        // Arrange
        var expectedOrders = new List<OrderResponse>
        {
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Paid",
                "USD",
                100m,
                new List<OrderItemResponse>
                {
                    new(Guid.NewGuid(), 100m, "USD", 1, 100m)
                },
                null)
        };

        _senderMock.Setup(m => m.Send(It.IsAny<GetFulfillmentOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<OrderResponse>>(expectedOrders));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.GetAsync("/api/v1/Orders/fulfillment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await response.Content.ReadFromJsonAsync<List<OrderResponse>>();
        orders.Should().NotBeNull();
        orders!.Should().HaveCount(1);
        orders[0].Status.Should().Be("Paid");
        orders[0].ShippingAddress.Should().BeNull();
    }

    [Fact]
    public async Task ShipOrder_WithSubmittedOrder_Returns400BadRequest()
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
        problemDetails.Detail.Should().Be(OrderErrors.InvalidStatusTransition.Message);
    }

    [Fact]
    public async Task ShipOrder_WithCancelledOrder_Returns400BadRequest()
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
        problemDetails.Detail.Should().Be(OrderErrors.InvalidStatusTransition.Message);
    }

    [Fact]
    public async Task ShipOrder_WithAlreadyShippedOrder_Returns400BadRequest()
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
        problemDetails.Detail.Should().Be(OrderErrors.InvalidStatusTransition.Message);
    }

    [Fact]
    public async Task ShipOrder_WithMissingOrder_Returns404NotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.ShipOrder.ShipOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var response = await client.PutAsync($"/api/v1/Orders/{orderId}/ship", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(404);
        problemDetails.Detail.Should().Be(OrderErrors.NotFound.Message);
    }

    [Fact]
    public async Task ShipOrder_ConcurrentDuplicateRequests_OnlyOneSucceedsAndOneRejectsDeterministically()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var callCount = 0;
        _senderMock.Setup(m => m.Send(It.Is<EnterpriseCommerce.Application.Orders.Commands.ShipOrder.ShipOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var current = Interlocked.Increment(ref callCount);
                return current == 1 ? Result.Success() : Result.Failure(OrderErrors.InvalidStatusTransition);
            });

        var client1 = _factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client1.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client2.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        // Act
        var task1 = client1.PutAsync($"/api/v1/Orders/{orderId}/ship", null);
        var task2 = client2.PutAsync($"/api/v1/Orders/{orderId}/ship", null);
        var responses = await Task.WhenAll(task1, task2);

        // Assert
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        statusCodes.Should().Contain(HttpStatusCode.OK);
        statusCodes.Should().Contain(HttpStatusCode.BadRequest);
    }
}
