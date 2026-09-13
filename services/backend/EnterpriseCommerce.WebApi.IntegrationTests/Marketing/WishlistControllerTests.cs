using EnterpriseCommerce.Application.Marketing.Wishlist.Commands.AddWishlistItem;
using EnterpriseCommerce.Application.Marketing.Wishlist.Commands.RemoveWishlistItem;
using EnterpriseCommerce.Application.Marketing.Wishlist.Queries.GetWishlist;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Domain.Primitives;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Marketing;

[Collection("IntegrationTests")]
public class WishlistControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public WishlistControllerTests(WebApplicationFactory<Program> factory)
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

    #region GET Tests

    [Fact]
    public async Task GetWishlist_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/wishlist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWishlist_AuthenticatedWithoutCustomerId_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");

        // Act
        var response = await client.GetAsync("/api/v1/wishlist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetWishlist_ValidCustomer_Returns200Ok()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var expectedResponse = new WishlistResponse(
            new List<WishlistItemResponse>
            {
                new(Guid.NewGuid(), DateTimeOffset.UtcNow)
            },
            1,
            25,
            1);

        _senderMock
            .Setup(s => s.Send(It.Is<GetWishlistQuery>(q => q.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(expectedResponse));

        // Act
        var response = await client.GetAsync("/api/v1/wishlist?page=1&pageSize=25");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<WishlistResponse>();
        content.Should().NotBeNull();
        content!.TotalCount.Should().Be(1);
    }

    #endregion

    #region POST Tests

    [Fact]
    public async Task PostItem_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var productId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/wishlist/items/{productId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostItem_MissingCustomerId_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        var productId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/api/v1/wishlist/items/{productId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostItem_ActiveProduct_Returns200Or204Success()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<AddWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var response = await client.PostAsync($"/api/v1/wishlist/items/{productId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostItem_InactiveProduct_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<AddWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.NotFound));

        // Act
        var response = await client.PostAsync($"/api/v1/wishlist/items/{productId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostItem_MissingProduct_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<AddWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.NotFound));

        // Act
        var response = await client.PostAsync($"/api/v1/wishlist/items/{productId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostItem_DuplicateItem_Returns409Conflict()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<AddWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(WishlistErrors.AlreadyExists));

        // Act
        var response = await client.PostAsync($"/api/v1/wishlist/items/{productId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion

    #region DELETE Tests

    [Fact]
    public async Task DeleteItem_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();
        var productId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/v1/wishlist/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteItem_MissingCustomerId_Returns403Forbidden()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        var productId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/api/v1/wishlist/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteItem_OwnedItem_Returns200Or204Success()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<RemoveWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var response = await client.DeleteAsync($"/api/v1/wishlist/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteItem_MissingItem_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<RemoveWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(WishlistErrors.NotFound));

        // Act
        var response = await client.DeleteAsync($"/api/v1/wishlist/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteItem_OtherCustomerItem_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<RemoveWishlistItemCommand>(c => c.CustomerId == customerId && c.ProductId == productId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(WishlistErrors.NotFound));

        // Act
        var response = await client.DeleteAsync($"/api/v1/wishlist/items/{productId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
