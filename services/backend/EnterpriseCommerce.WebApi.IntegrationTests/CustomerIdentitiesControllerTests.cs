using EnterpriseCommerce.Application.Identity.Commands.ResolveCustomerIdentity;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Identity;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
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
public class CustomerIdentitiesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public CustomerIdentitiesControllerTests(WebApplicationFactory<Program> factory)
    {
        _senderMock = new Mock<ISender>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", "Server=localhost;Database=Test;Uid=test;Pwd=test;");
            builder.UseSetting("Authentication:Authority", "https://identity.enterprisecommerce.test/");
            builder.UseSetting("Authentication:Audience", "enterprise_commerce_api_test");

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
    public async Task Post_Resolve_WithoutAuthToken_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_Resolve_WithAuthToken_WithoutIdentityResolveScope_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        // No X-Test-Scope header provided

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithAuthToken_WithWrongScope_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "read:orders write:orders");

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithIdentityResolveScope_Returns200OK_WithCustomerId()
    {
        // Arrange
        var subject = "auth0|test-user-valid";
        var expectedCustomerId = Guid.NewGuid();
        var expectedIssuer = "https://identity.enterprisecommerce.test/";

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == expectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");

        var request = new ResolveCustomerIdentityRequest(subject);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadFromJsonAsync<CustomerIdentityResponse>();
        responseBody.Should().NotBeNull();
        responseBody!.CustomerId.Should().Be(expectedCustomerId);
    }

    [Fact]
    public async Task Post_Resolve_WithMultiScopeContainingIdentityResolve_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-multi-scope";
        var expectedCustomerId = Guid.NewGuid();
        var expectedIssuer = "https://identity.enterprisecommerce.test/";

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == expectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "read:users identity:resolve write:reports");

        var request = new ResolveCustomerIdentityRequest(subject);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadFromJsonAsync<CustomerIdentityResponse>();
        responseBody.Should().NotBeNull();
        responseBody!.CustomerId.Should().Be(expectedCustomerId);
    }

    [Fact]
    public async Task Post_Resolve_WhenHandlerReturnsFailure_Returns400BadRequest()
    {
        // Arrange
        var subject = "invalid-subject";
        var expectedIssuer = "https://identity.enterprisecommerce.test/";
        var error = new Error("Identity.InvalidSubject", "Subject is invalid.");

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == expectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(error));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");

        var request = new ResolveCustomerIdentityRequest(subject);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
        problem.Detail.Should().Be("Subject is invalid.");
    }
}
