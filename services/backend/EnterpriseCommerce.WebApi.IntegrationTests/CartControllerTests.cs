using EnterpriseCommerce.Application.Catalog;
using EnterpriseCommerce.Application.Orders.Commands.AddItemToCart;
using EnterpriseCommerce.Application.Orders.Commands.RemoveCartItem;
using EnterpriseCommerce.Application.Orders.Commands.UpdateCartItemQuantity;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Cart;
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

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class CartControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public CartControllerTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task GetCart_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostItem_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new AddCartItemRequest(Guid.NewGuid(), 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutItemQuantity_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new UpdateCartItemQuantityRequest(2);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{Guid.NewGuid()}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteItem_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"/api/v1/cart/items/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCart_WithoutCustomerIdClaim_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        // Do not add X-Test-User-Id header

        // Act
        var response = await client.GetAsync("/api/v1/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCart_AuthenticatedWithNoCart_Returns200OK_EmptyCart()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        _senderMock.Setup(m => m.Send(
                It.Is<GetCartQuery>(q => q.CustomerId == customerId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(CartResponse.Empty()));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        // Act
        var response = await client.GetAsync("/api/v1/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Id.Should().BeNull();
        cart.Items.Should().BeEmpty();
        cart.TotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task PostItem_AuthenticatedFirstAdd_Returns200OK_Success()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var expectedCart = new CartResponse(
            Guid.NewGuid(),
            "TWD",
            300m,
            new List<CartItemResponse>
            {
                new(productId, 150m, "TWD", 2, 300m)
            });

        _senderMock.Setup(m => m.Send(
                It.Is<AddItemToCartCommand>(c => c.CustomerId == customerId && c.ProductId == productId && c.Quantity == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCart));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new AddCartItemRequest(productId, 2);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Id.Should().Be(expectedCart.Id);
        cart.Items.Should().HaveCount(1);
        cart.TotalAmount.Should().Be(300m);
    }

    [Fact]
    public async Task GetCart_AfterAdd_Returns200OK_WithItemAndTotal()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var expectedCart = new CartResponse(
            Guid.NewGuid(),
            "USD",
            100m,
            new List<CartItemResponse>
            {
                new(productId, 50m, "USD", 2, 100m)
            });

        _senderMock.Setup(m => m.Send(
                It.Is<GetCartQuery>(q => q.CustomerId == customerId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCart));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        // Act
        var response = await client.GetAsync("/api/v1/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Items.Should().HaveCount(1);
        cart.Items.First().ProductId.Should().Be(productId);
        cart.TotalAmount.Should().Be(100m);
    }

    [Fact]
    public async Task PostItem_SameProductAddedTwice_Returns200OK_WithIncreasedQuantity()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var updatedCart = new CartResponse(
            Guid.NewGuid(),
            "USD",
            200m,
            new List<CartItemResponse>
            {
                new(productId, 50m, "USD", 4, 200m)
            });

        _senderMock.Setup(m => m.Send(
                It.Is<AddItemToCartCommand>(c => c.CustomerId == customerId && c.ProductId == productId && c.Quantity == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(updatedCart));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new AddCartItemRequest(productId, 2);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(4);
        cart.TotalAmount.Should().Be(200m);
    }

    [Fact]
    public async Task PutItemQuantity_WithValidQuantity_Returns200OK()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<UpdateCartItemQuantityCommand>(c => c.CustomerId == customerId && c.ProductId == productId && c.Quantity == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new UpdateCartItemQuantityRequest(5);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{productId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteItem_WithExistingItem_Returns200OK()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<RemoveCartItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        // Act
        var response = await client.DeleteAsync($"/api/v1/cart/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCart_SecondCustomer_ReturnsOnlyOwnEmptyCart()
    {
        // Arrange
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<GetCartQuery>(q => q.CustomerId == customerB),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(CartResponse.Empty()));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        // Act
        var response = await client.GetAsync("/api/v1/cart");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResponse>();
        cart.Should().NotBeNull();
        cart!.Items.Should().BeEmpty();
        _senderMock.Verify(m => m.Send(It.Is<GetCartQuery>(q => q.CustomerId == customerA), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostItem_InactiveProduct_Returns400BadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<AddItemToCartCommand>(c => c.CustomerId == customerId && c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CartResponse>(ProductErrors.NotActive));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new AddCartItemRequest(productId, 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(ProductErrors.NotActive.Message);
    }

    [Fact]
    public async Task PostItem_NonExistentProduct_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<AddItemToCartCommand>(c => c.CustomerId == customerId && c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CartResponse>(ProductErrors.NotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new AddCartItemRequest(productId, 1);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutItemQuantity_NonExistentItem_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
                It.Is<UpdateCartItemQuantityCommand>(c => c.CustomerId == customerId && c.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.ItemNotFound));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new UpdateCartItemQuantityRequest(3);

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/cart/items/{productId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
