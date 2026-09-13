using EnterpriseCommerce.Application.Marketing.Reviews.Commands.CreateProductReview;
using EnterpriseCommerce.Application.Marketing.Reviews.Queries.GetProductReviews;
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
public class ProductReviewsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public ProductReviewsControllerTests(WebApplicationFactory<Program> factory)
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
    public async Task GetReviews_Anonymous_WhenProductIsActive_Returns200Ok()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        var queryResponse = new ProductReviewsResponse(
            new List<ProductReviewItemResponse>
            {
                new(5, "很棒的商品", DateTimeOffset.UtcNow)
            },
            1,
            10,
            1,
            5.0);

        _senderMock
            .Setup(s => s.Send(
                It.Is<GetProductReviewsQuery>(q => q.ProductId == productId && q.Page == 1 && q.PageSize == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(queryResponse));

        // Act
        var response = await client.GetAsync($"/api/v1/products/{productId}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        content.Should().NotBeNull();
        content!.TotalCount.Should().Be(1);
        content.AverageRating.Should().Be(5.0);
        content.Items.Should().HaveCount(1);
        content.Items[0].Rating.Should().Be(5);
        content.Items[0].Comment.Should().Be("很棒的商品");

        // 驗證公開 DTO 絕不包含內部識別碼
        var json = await response.Content.ReadAsStringAsync();
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
        var item = jsonDoc.RootElement.GetProperty("items")[0];
        item.TryGetProperty("customerId", out _).Should().BeFalse();
        item.TryGetProperty("id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetReviews_Anonymous_WhenNoReviews_Returns200OkWithNullAverageRating()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        var queryResponse = new ProductReviewsResponse(
            new List<ProductReviewItemResponse>(),
            1,
            10,
            0,
            null);

        _senderMock
            .Setup(s => s.Send(
                It.Is<GetProductReviewsQuery>(q => q.ProductId == productId && q.Page == 1 && q.PageSize == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(queryResponse));

        // Act
        var response = await client.GetAsync($"/api/v1/products/{productId}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        content.Should().NotBeNull();
        content!.TotalCount.Should().Be(0);
        content.AverageRating.Should().BeNull();
        content.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReviews_WhenProductMissingOrInactive_Returns404NotFound()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();

        _senderMock
            .Setup(s => s.Send(
                It.Is<GetProductReviewsQuery>(q => q.ProductId == productId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ProductReviewsResponse>(ProductErrors.NotFound));

        // Act
        var response = await client.GetAsync($"/api/v1/products/{productId}/reviews");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST Tests

    [Fact]
    public async Task PostReview_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        var body = new { rating = 5, comment = "讚" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostReview_AuthenticatedWithoutCustomerId_Returns403Forbidden()
    {
        // Arrange
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        var body = new { rating = 5, comment = "讚" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostReview_ValidCustomer_WhenSuccess_Returns200Ok()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(
                It.Is<CreateProductReviewCommand>(c =>
                    c.CustomerId == customerId &&
                    c.ProductId == productId &&
                    c.Rating == 5 &&
                    c.Comment == "極佳品質"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var body = new { rating = 5, comment = "極佳品質" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostReview_WhenInvalidRating_Returns400BadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(
                It.IsAny<CreateProductReviewCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ReviewErrors.InvalidRating));

        var body = new { rating = 6, comment = "評分超標" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostReview_WhenInvalidComment_Returns400BadRequest()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(
                It.IsAny<CreateProductReviewCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ReviewErrors.InvalidComment));

        var body = new { rating = 5, comment = "" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostReview_WhenProductMissingOrInactive_Returns404NotFound()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(
                It.IsAny<CreateProductReviewCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ProductErrors.NotFound));

        var body = new { rating = 5, comment = "好商品" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostReview_WhenDuplicateReview_Returns409Conflict()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(
                It.IsAny<CreateProductReviewCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(ReviewErrors.AlreadyExists));

        var body = new { rating = 5, comment = "再次評論" };

        // Act
        var response = await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion
}
