using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EnterpriseCommerce.Application.Payments.Commands.AdminRefundPayment;
using EnterpriseCommerce.Domain.Payments;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Payments;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Payments;

[Collection("IntegrationTests")]
public class AdminRefundPaymentsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public AdminRefundPaymentsEndpointTests(WebApplicationFactory<Program> factory)
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
        string? sub = null,
        string? issuer = null,
        bool noSub = false,
        bool noIssuer = false)
    {
        var client = _factory.CreateClient();
        if (role != null || sub != null || noSub || noIssuer)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
            if (role != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Role", role);
            }
            if (sub != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Sub", sub);
            }
            if (issuer != null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Issuer", issuer);
            }
            if (noSub)
            {
                client.DefaultRequestHeaders.Add("X-Test-No-Sub", "true");
            }
            if (noIssuer)
            {
                client.DefaultRequestHeaders.Add("X-Test-No-Issuer", "true");
            }
        }
        return client;
    }

    [Fact]
    public async Task RefundPayment_Unauthenticated_Returns401_CommandNotSent()
    {
        // Arrange
        var client = CreateClientWithRole();
        var paymentAttemptId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/payments/{paymentAttemptId}/refund",
            new AdminRefundPaymentRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(s => s.Send(It.IsAny<AdminRefundPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundPayment_AuthenticatedNonAdmin_Returns403()
    {
        // Arrange
        var client = CreateClientWithRole(role: "Customer");
        var paymentAttemptId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/payments/{paymentAttemptId}/refund",
            new AdminRefundPaymentRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _senderMock.Verify(s => s.Send(It.IsAny<AdminRefundPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundPayment_AdminMissingIssuer_Returns401_CommandNotSent()
    {
        // Arrange
        var client = CreateClientWithRole(role: "Admin", sub: "auth0|admin-user", noIssuer: true);
        var paymentAttemptId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/payments/{paymentAttemptId}/refund",
            new AdminRefundPaymentRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(s => s.Send(It.IsAny<AdminRefundPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundPayment_AdminMissingSubject_Returns401_CommandNotSent()
    {
        // Arrange
        var client = CreateClientWithRole(role: "Admin", issuer: "https://auth.example.com/", noSub: true);
        var paymentAttemptId = Guid.NewGuid();

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/payments/{paymentAttemptId}/refund",
            new AdminRefundPaymentRequest("Reason"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _senderMock.Verify(s => s.Send(It.IsAny<AdminRefundPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefundPayment_AdminValidActor_PassesClaimsToCommand()
    {
        // Arrange
        var client = CreateClientWithRole(role: "Admin", sub: "auth0|admin-trusted-1", issuer: "https://auth.enterprisecommerce.com/");
        var paymentAttemptId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<AdminRefundPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AdminRefundPaymentResult(
                paymentAttemptId,
                AdminRefundOutcome.RefundCompleted,
                PaymentRefundStatus.Succeeded)));

        // Act
        var response = await client.PostAsJsonAsync(
            $"/api/v1/admin/payments/{paymentAttemptId}/refund",
            new AdminRefundPaymentRequest("Customer requested return"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _senderMock.Verify(s => s.Send(
            It.Is<AdminRefundPaymentCommand>(cmd =>
                cmd.PaymentAttemptId == paymentAttemptId &&
                cmd.ActorSubject == "auth0|admin-trusted-1" &&
                cmd.ActorIssuer == "https://auth.enterprisecommerce.com/" &&
                cmd.Reason == "Customer requested return"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void RequestContract_MechanicallyAssertExposesOnlyReason()
    {
        var properties = typeof(AdminRefundPaymentRequest).GetProperties();
        properties.Should().ContainSingle();
        properties[0].Name.Should().Be("Reason");
    }

    [Fact]
    public async Task RefundPayment_ExtraJsonFields_AmountAndProviderIgnored_SensitiveFieldsAbsentInResponse()
    {
        // Arrange
        var client = CreateClientWithRole(role: "Admin", sub: "auth0|admin-trusted-1", issuer: "https://auth.enterprisecommerce.com/");
        var paymentAttemptId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.IsAny<AdminRefundPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AdminRefundPaymentResult(
                paymentAttemptId,
                AdminRefundOutcome.RefundCompleted,
                PaymentRefundStatus.Succeeded)));

        // Send malicious/extra client payload attempting to inject amount, provider, actor
        var maliciousJson = JsonSerializer.Serialize(new
        {
            reason = "Valid Reason",
            amount = 1.00m,
            currency = "USD",
            provider = "Stripe",
            providerTransactionId = "HACKED_TX",
            actorIssuer = "attacker",
            actorSubject = "attacker_sub"
        });

        var content = new StringContent(maliciousJson, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(
            $"/api/v1/admin/payments/{paymentAttemptId}/refund",
            content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Command receives verified server/token claims, NOT payload injected values
        _senderMock.Verify(s => s.Send(
            It.Is<AdminRefundPaymentCommand>(cmd =>
                cmd.PaymentAttemptId == paymentAttemptId &&
                cmd.ActorIssuer == "https://auth.enterprisecommerce.com/" &&
                cmd.ActorSubject == "auth0|admin-trusted-1" &&
                cmd.Reason == "Valid Reason"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify response payload contains NO sensitive provider identifiers
        var responseString = await response.Content.ReadAsStringAsync();
        responseString.Should().NotContain("ProviderTransactionId");
        responseString.Should().NotContain("ProviderAuthorizationReference");
        responseString.Should().NotContain("CheckMacValue");
        responseString.Should().NotContain("MerchantTradeNo");
    }
}
