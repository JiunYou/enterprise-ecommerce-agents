using EnterpriseCommerce.Application.Payments.Commands.InitiatePayment;
using EnterpriseCommerce.Application.Payments.Commands.ProcessPaymentWebhook;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Payments;
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
using EnterpriseCommerce.Application.Payments;

namespace EnterpriseCommerce.WebApi.IntegrationTests;

[Collection("IntegrationTests")]
public class PaymentsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public PaymentsControllerTests(WebApplicationFactory<Program> factory)
    {


        _senderMock = new Mock<ISender>();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", "Server=localhost;Database=Test;Uid=test;Pwd=test;");
            builder.UseSetting("EnableDummyWebhook", "true");
            builder.UseSetting("DummyWebhookSecret", "dummy-secret-123");

            builder.ConfigureTestServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(EnterpriseCommerce.WebApi.IntegrationTests.Controllers.TestDummyWebhookController).Assembly);
                services.AddScoped<ISender>(_ => _senderMock.Object);

                // Add TestAuthHandler
                services.AddAuthentication(TestAuthHandler.DefaultScheme)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.DefaultScheme, options => { });
            });
        });
    }

    [Fact]
    public async Task InitiatePayment_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();
        var request = new InitiatePaymentRequest(orderId, idempotencyKey);

        var expectedResponse = new InitiatePaymentResponse("provider_tx_id_123", "https://checkout.url");
        var customerIdStr = Guid.NewGuid().ToString();
        _senderMock.Setup(x => x.Send(
                It.Is<InitiatePaymentCommand>(c => c.OrderId == orderId && c.IdempotencyKey == idempotencyKey && c.CustomerId == Guid.Parse(customerIdStr)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResponse));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerIdStr);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<InitiatePaymentResponse>();
        result.Should().NotBeNull();
        result!.ProviderTransactionId.Should().Be("provider_tx_id_123");
    }

    [Fact]
    public async Task ProcessDummyWebhook_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var command = new ProcessPaymentWebhookCommand(
            Guid.NewGuid(),
            "DummyProvider",
            "event_123",
            "tx_123",
            100m,
            "USD",
            true);

        _senderMock.Setup(x => x.Send(
                It.Is<ProcessPaymentWebhookCommand>(c => c.ProviderEventId == "event_123"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dummy-Signature", "dummy-secret-123");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/payments/webhook/dummy", command);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InitiatePayment_Unauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var request = new InitiatePaymentRequest(Guid.NewGuid(), Guid.NewGuid());
        var client = _factory.CreateClient();
        // Do NOT set Authorization header

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InitiatePayment_NotOwner_ReturnsNotFound()
    {
        // Arrange
        var request = new InitiatePaymentRequest(Guid.NewGuid(), Guid.NewGuid());

        var customerIdStr = Guid.NewGuid().ToString();
        _senderMock.Setup(x => x.Send(
                It.Is<InitiatePaymentCommand>(c => c.CustomerId == Guid.Parse(customerIdStr)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<InitiatePaymentResponse>(new Error("Order.NotFound", "Order was not found"))); // Represents what handler does when CustomerId mismatch

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerIdStr);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/payments/initiate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
