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
    private const string ExpectedClientId = "identity-resolver-m2m-test-client";
    private const string ExpectedIssuer = "https://identity.enterprisecommerce.test/";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public CustomerIdentitiesControllerTests(WebApplicationFactory<Program> factory)
    {
        _senderMock = new Mock<ISender>();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", "Server=localhost;Database=Test;Uid=test;Pwd=test;");
            builder.UseSetting("Authentication:Authority", ExpectedIssuer);
            builder.UseSetting("Authentication:Audience", "enterprise_commerce_api_test");
            builder.UseSetting("Authentication:IdentityResolverClientId", ExpectedClientId);

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
    public async Task Post_Resolve_WithScope_WithoutClientIdentity_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        // No X-Test-Azp or X-Test-ClientId provided

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithScope_WithWrongAzp_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", "wrong-client-id");

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithScope_WithWrongClientId_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-ClientId", "wrong-client-id");

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithScope_WithConflictingClientIdentity_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);
        client.DefaultRequestHeaders.Add("X-Test-ClientId", "conflicting-other-client");

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithCorrectClient_WithoutScope_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);
        // No scope header

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithCorrectClient_WithWrongScope_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "read:orders write:orders");

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Resolve_WithScope_WithCorrectAzp_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-valid";
        var expectedCustomerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);

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
    public async Task Post_Resolve_WithScope_WithCorrectClientId_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-valid-client-id";
        var expectedCustomerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-ClientId", ExpectedClientId);

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
    public async Task Post_Resolve_WithScope_WithMatchingAzpAndClientId_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-matching-azp-client-id";
        var expectedCustomerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);
        client.DefaultRequestHeaders.Add("X-Test-ClientId", ExpectedClientId);

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
    public async Task Post_Resolve_WithMultiScope_WithCorrectClient_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-multi-scope";
        var expectedCustomerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "read:users identity:resolve write:reports");
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);

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
        var error = new Error("Identity.InvalidSubject", "Subject is invalid.");

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>(error));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);

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

    [Fact]
    public async Task Post_Resolve_WithPermissionsClaim_WithCorrectAzp_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-permissions-claim";
        var expectedCustomerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", ExpectedClientId);

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
    public async Task Post_Resolve_WithScope_WithM2MClientSub_Returns200OK()
    {
        // Arrange
        var subject = "auth0|test-user-m2m-sub";
        var expectedCustomerId = Guid.NewGuid();

        _senderMock.Setup(m => m.Send(
            It.Is<ResolveCustomerIdentityCommand>(c => c.Issuer == ExpectedIssuer && c.Subject == subject),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedCustomerId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Scope", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Sub", $"{ExpectedClientId}@clients");

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
    public async Task Post_Resolve_WithPermissionsClaim_WithoutCorrectClient_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "identity:resolve");
        client.DefaultRequestHeaders.Add("X-Test-Azp", "wrong-client-id");

        var request = new ResolveCustomerIdentityRequest("auth0|test-user");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/internal/customer-identities/resolve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
