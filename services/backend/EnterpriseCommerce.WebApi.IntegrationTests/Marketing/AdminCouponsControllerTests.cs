using EnterpriseCommerce.Application.Marketing.Coupons;
using EnterpriseCommerce.Application.Marketing.Coupons.Commands.CreateCoupon;
using EnterpriseCommerce.Application.Marketing.Coupons.Commands.DeactivateCoupon;
using EnterpriseCommerce.Application.Marketing.Coupons.Queries.GetCoupons;
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
public class AdminCouponsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public AdminCouponsControllerTests(WebApplicationFactory<Program> factory)
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

    private HttpClient CreateAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        return client;
    }

    private HttpClient CreateCustomerClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme);
        client.DefaultRequestHeaders.Add("X-Test-User-Id", Guid.NewGuid().ToString());
        client.DefaultRequestHeaders.Add("X-Test-Role", "Customer");
        return client;
    }


    [Fact]
    public async Task GetCoupons_Anonymous_Returns401Unauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/coupons");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCoupons_NonAdmin_Returns403Forbidden()
    {
        // Arrange
        var client = CreateCustomerClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/coupons");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCoupons_Admin_Returns200OkWithList()
    {
        // Arrange
        var client = CreateAdminClient();
        var couponList = new List<CouponResponse>
        {
            new(Guid.NewGuid(), "WELCOME100", 100m, "USD", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7), true, DateTimeOffset.UtcNow)
        };

        _senderMock.Setup(s => s.Send(It.IsAny<GetCouponsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CouponResponse>>(couponList));

        // Act
        var response = await client.GetAsync("/api/v1/admin/coupons");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CouponResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCount(1);
        body[0].Code.Should().Be("WELCOME100");
    }

    [Fact]
    public async Task CreateCoupon_Admin_Returns200OkOr201Created()
    {
        // Arrange
        var client = CreateAdminClient();
        var now = DateTimeOffset.UtcNow;
        var createdCoupon = new CouponResponse(Guid.NewGuid(), "WELCOME100", 100m, "USD", now, now.AddDays(7), true, now);

        _senderMock.Setup(s => s.Send(It.IsAny<CreateCouponCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(createdCoupon));

        var request = new
        {
            code = "WELCOME100",
            discountAmount = 100m,
            currency = "USD",
            startsAt = now,
            expiresAt = now.AddDays(7)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/admin/coupons", request);

        // Assert
        response.StatusCode.Should().Match(s => s == HttpStatusCode.OK || s == HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CouponResponse>();
        body.Should().NotBeNull();
        body!.Code.Should().Be("WELCOME100");
    }

    [Fact]
    public async Task CreateCoupon_DuplicateCode_Returns409Conflict()
    {
        // Arrange
        var client = CreateAdminClient();
        var now = DateTimeOffset.UtcNow;

        _senderMock.Setup(s => s.Send(It.IsAny<CreateCouponCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CouponResponse>(CouponErrors.Conflict));

        var request = new
        {
            code = "DUPLICATE",
            discountAmount = 100m,
            currency = "USD",
            startsAt = now,
            expiresAt = now.AddDays(7)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/admin/coupons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCoupon_InvalidRequest_Returns400BadRequest()
    {
        // Arrange
        var client = CreateAdminClient();
        var now = DateTimeOffset.UtcNow;

        _senderMock.Setup(s => s.Send(It.IsAny<CreateCouponCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CouponResponse>(CouponErrors.InvalidCode));

        var request = new
        {
            code = "INVALID CODE",
            discountAmount = 100m,
            currency = "USD",
            startsAt = now,
            expiresAt = now.AddDays(7)
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/admin/coupons", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeactivateCoupon_Admin_Returns200Ok()
    {
        // Arrange
        var client = CreateAdminClient();
        var couponId = Guid.NewGuid();

        _senderMock.Setup(s => s.Send(It.Is<DeactivateCouponCommand>(c => c.Id == couponId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var response = await client.PostAsync($"/api/v1/admin/coupons/{couponId}/deactivate", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
