using EnterpriseCommerce.Application.Orders.Commands.ApplyCouponToCart;
using EnterpriseCommerce.Application.Orders.Commands.RemoveCouponFromCart;
using EnterpriseCommerce.Application.Orders.Queries.GetCart;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Cart;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

[Collection("IntegrationTests")]
public class CartCouponEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public CartCouponEndpointTests(WebApplicationFactory<Program> factory)
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

    private HttpClient CreateCustomerClient(Guid customerId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        return client;
    }

    [Fact]
    public async Task ApplyCoupon_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/cart/coupon", new { code = "WELCOME100" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApplyCoupon_MissingCustomerIdClaim_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        // No X-Test-User-Id header

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/cart/coupon", new { code = "WELCOME100" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }


    [Fact]
    public async Task ApplyCoupon_ValidRequest_Returns200OkWithCartResponse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var cartResponse = new CartResponse(
            Guid.NewGuid(),
            "USD",
            900m,
            Array.Empty<CartItemResponse>(),
            1000m,
            100m,
            "WELCOME100");

        _senderMock.Setup(s => s.Send(It.Is<ApplyCouponToCartCommand>(c => c.CustomerId == customerId && c.Code == "WELCOME100"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(cartResponse));

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/cart/coupon", new { code = "WELCOME100" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.AppliedCouponCode.Should().Be("WELCOME100");
        body.TotalAmount.Should().Be(900m);
    }

    [Fact]
    public async Task ApplyCoupon_InvalidCoupon_Returns400BadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        _senderMock.Setup(s => s.Send(It.IsAny<ApplyCouponToCartCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CartResponse>(CouponErrors.InvalidOrUnavailable));

        // Act
        var response = await client.PutAsJsonAsync("/api/v1/cart/coupon", new { code = "INVALID" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RemoveCoupon_ValidRequest_Returns200OkWithCartResponse()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = CreateCustomerClient(customerId);

        var cartResponse = new CartResponse(
            Guid.NewGuid(),
            "USD",
            1000m,
            Array.Empty<CartItemResponse>(),
            1000m,
            0m,
            null);

        _senderMock.Setup(s => s.Send(It.Is<RemoveCouponFromCartCommand>(c => c.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(cartResponse));

        // Act
        var response = await client.DeleteAsync("/api/v1/cart/coupon");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.AppliedCouponCode.Should().BeNull();
        body.TotalAmount.Should().Be(1000m);
    }

    [Fact]
    public async Task RemoveCoupon_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/api/v1/cart/coupon");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
