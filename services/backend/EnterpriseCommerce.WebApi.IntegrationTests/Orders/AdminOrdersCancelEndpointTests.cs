using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using EnterpriseCommerce.Application.Orders.Commands.AdminCancelOrder;
using EnterpriseCommerce.Domain.Orders;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Orders;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Orders;

[Collection("IntegrationTests")]
public class AdminOrdersCancelEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public AdminOrdersCancelEndpointTests(WebApplicationFactory<Program> factory)
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
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, _ => { });

                services.AddSingleton(_senderMock.Object);
            });
        });
    }

    private HttpClient CreateClientWithRole(
        string? role = null,
        Guid? customerId = null,
        string? sub = null,
        string? issuer = null,
        bool noSub = false,
        bool noIssuer = false)
    {
        var client = _factory.CreateClient();
        if (role != null || customerId != null || sub != null || noSub || noIssuer)
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
            if (noSub)
            {
                client.DefaultRequestHeaders.Add("X-Test-No-Sub", "true");
            }
            else if (sub != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
            }
            if (noIssuer)
            {
                client.DefaultRequestHeaders.Add("X-Test-No-Issuer", "true");
            }
            else if (issuer != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Issuer", issuer);
            }
        }
        return client;
    }

    [Fact]
    public async Task CancelOrder_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Some reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_WithCustomerToken_Returns403Forbidden()
    {
        // Arrange
        var client = CreateClientWithRole("Customer", customerId: Guid.NewGuid());
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Some reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_WithCustomerIdOnly_Returns403Forbidden()
    {
        // Arrange
        var client = CreateClientWithRole(role: null, customerId: Guid.NewGuid());
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Some reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_AsAdmin_WithValidActor_SendsCommandWithExtractedActor_AndReturns200Ok()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = CreateClientWithRole("Admin", sub: "auth0|admin-42", issuer: "https://auth.company.com/");
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Customer requested cancellation"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _senderMock.Verify(m => m.Send(It.Is<AdminCancelOrderCommand>(c =>
            c.OrderId == orderId &&
            c.ActorIssuer == "https://auth.company.com/" &&
            c.ActorSubject == "auth0|admin-42" &&
            c.Reason == "Customer requested cancellation"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrder_AsAdmin_MissingSubject_Returns401Unauthorized_AndNeverSendsCommand()
    {
        // Arrange
        _senderMock.Reset();
        var client = CreateClientWithRole("Admin", noSub: true);
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Valid reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_AsAdmin_MissingIssuer_Returns401Unauthorized_AndNeverSendsCommand()
    {
        // Arrange
        _senderMock.Reset();
        var client = CreateClientWithRole("Admin", noIssuer: true);
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Valid reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_ActorSpoofing_IgnoresRequestBodyActorFields_UsesClaimsOnly()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = CreateClientWithRole("Admin", sub: "auth0|trusted-admin", issuer: "https://auth.enterprisecommerce.com/");
        var orderId = Guid.NewGuid();

        var spoofedJson = """
        {
            "reason": "Legitimate reason",
            "actorIssuer": "https://evil.com/",
            "actorSubject": "evil-actor-id",
            "cancelledAt": "2000-01-01T00:00:00Z"
        }
        """;

        var content = new StringContent(spoofedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PutAsync($"/api/v1/admin/orders/{orderId}/cancel", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _senderMock.Verify(m => m.Send(It.Is<AdminCancelOrderCommand>(c =>
            c.OrderId == orderId &&
            c.ActorIssuer == "https://auth.enterprisecommerce.com/" &&
            c.ActorSubject == "auth0|trusted-admin" &&
            c.Reason == "Legitimate reason"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WhenOrderNotFound_Returns404NotFound()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.NotFound));

        var client = CreateClientWithRole("Admin");
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelOrder_WhenOrderIsPaid_Returns400BadRequest()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("Order.CannotCancelPaidOrder", "Paid orders cannot be cancelled by this operation.")));

        var client = CreateClientWithRole("Admin");
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Paid orders cannot be cancelled by this operation.");
    }

    [Fact]
    public async Task CancelOrder_WhenOrderIsShipped_Returns400BadRequest()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.InvalidStatusTransition));

        var client = CreateClientWithRole("Admin");
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelOrder_WhenOrderIsAlreadyCancelled_Returns400BadRequest()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(OrderErrors.InvalidStatusTransition));

        var client = CreateClientWithRole("Admin");
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CancelOrder_WhenOptimisticConcurrencyConflict_Returns409Conflict()
    {
        // Arrange
        _senderMock.Reset();
        _senderMock.Setup(m => m.Send(It.IsAny<AdminCancelOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new Error("Order.ConcurrencyConflict", "The order was modified by another operation.")));

        var client = CreateClientWithRole("Admin");
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/cancel", new AdminCancelOrderRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
