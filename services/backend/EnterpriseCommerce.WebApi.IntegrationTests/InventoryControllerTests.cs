using EnterpriseCommerce.Application.Inventory.Commands.ReserveInventory;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Inventory;
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
public class InventoryControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public InventoryControllerTests(WebApplicationFactory<Program> factory)
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
        var request = new ReserveInventoryRequest(Guid.NewGuid(), Guid.NewGuid(), 5);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Inventory/reserve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WithValidPayload_Returns200OK()
    {
        // Arrange
        _senderMock.Setup(m => m.Send(It.IsAny<ReserveInventoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var request = new ReserveInventoryRequest(Guid.NewGuid(), Guid.NewGuid(), 5);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Inventory/reserve", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WithInvalidCommand_Returns400BadRequest_AndRFC7807ProblemDetails()
    {
        // Arrange
        var error = new Error("Inventory.Quantity", "Quantity must be greater than 0.");
        var request = new ReserveInventoryRequest(Guid.NewGuid(), Guid.NewGuid(), 0);
        _senderMock.Setup(m => m.Send(It.IsAny<ReserveInventoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(error));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);

        var requestToPost = new ReserveInventoryRequest(Guid.NewGuid(), Guid.NewGuid(), 0);

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/Inventory/reserve", requestToPost);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(400);
        problemDetails.Title.Should().Be("Bad Request");
        problemDetails.Detail.Should().Be("Quantity must be greater than 0.");
    }
}
