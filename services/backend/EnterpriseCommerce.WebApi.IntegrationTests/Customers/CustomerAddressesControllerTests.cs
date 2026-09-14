using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.CreateCustomerAddress;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.DeleteCustomerAddress;
using EnterpriseCommerce.Application.Customers.Addresses.Commands.UpdateCustomerAddress;
using EnterpriseCommerce.Application.Customers.Addresses.Queries.GetCustomerAddresses;
using EnterpriseCommerce.Domain.Customers;
using EnterpriseCommerce.Domain.Primitives;
using EnterpriseCommerce.WebApi.Contracts.Customers;
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

namespace EnterpriseCommerce.WebApi.IntegrationTests.Customers;

[Collection("IntegrationTests")]
public class CustomerAddressesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<ISender> _senderMock;

    public CustomerAddressesControllerTests(WebApplicationFactory<Program> factory)
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

    #region GET /api/v1/customer/addresses

    [Fact]
    public async Task GetAddresses_Anonymous_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAddresses_AuthenticatedWithoutCustomerId_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");

        var response = await client.GetAsync("/api/v1/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAddresses_ValidCustomer_Returns200WithAddresses()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var expectedResponses = new List<CustomerAddressResponse>
        {
            new(Guid.NewGuid(), "王小明", "0912345678", "TW", "100", "台北市", "忠孝西路", null)
        };

        _senderMock
            .Setup(s => s.Send(It.Is<GetCustomerAddressesQuery>(q => q.CustomerId == customerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<CustomerAddressResponse>>(expectedResponses));

        var response = await client.GetAsync("/api/v1/customer/addresses");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CustomerAddressResponse>>();
        body.Should().NotBeNull();
        body!.Should().HaveCount(1);
        body[0].RecipientName.Should().Be("王小明");
    }

    #endregion

    #region POST /api/v1/customer/addresses

    [Fact]
    public async Task CreateAddress_Anonymous_Returns401Unauthorized()
    {
        var client = _factory.CreateClient();
        var request = new CreateCustomerAddressRequest(
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        var response = await client.PostAsJsonAsync("/api/v1/customer/addresses", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAddress_AuthenticatedWithoutCustomerId_Returns403Forbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        var request = new CreateCustomerAddressRequest(
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        var response = await client.PostAsJsonAsync("/api/v1/customer/addresses", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateAddress_ValidRequest_ReturnsSuccess()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new CreateCustomerAddressRequest(
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            "3 樓之 1");

        var addressId = Guid.NewGuid();
        var createdResponse = new CustomerAddressResponse(
            addressId,
            "王小明",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路一段",
            "3 樓之 1");

        _senderMock
            .Setup(s => s.Send(It.Is<CreateCustomerAddressCommand>(c =>
                c.CustomerId == customerId &&
                c.RecipientName == request.RecipientName &&
                c.Phone == request.Phone &&
                c.CountryCode == request.CountryCode &&
                c.PostalCode == request.PostalCode &&
                c.City == request.City &&
                c.AddressLine1 == request.AddressLine1 &&
                c.AddressLine2 == request.AddressLine2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(createdResponse));

        var response = await client.PostAsJsonAsync("/api/v1/customer/addresses", request);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(addressId);
        body.RecipientName.Should().Be("王小明");
    }

    [Fact]
    public async Task CreateAddress_InvalidDomainAddress_Returns400BadRequest()
    {
        var customerId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new CreateCustomerAddressRequest(
            "",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        _senderMock
            .Setup(s => s.Send(It.IsAny<CreateCustomerAddressCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.InvalidRecipientName));

        var response = await client.PostAsJsonAsync("/api/v1/customer/addresses", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region DELETE /api/v1/customer/addresses/{addressId}

    [Fact]
    public async Task DeleteAddress_Anonymous_Returns401Unauthorized()
    {
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/customer/addresses/{addressId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteAddress_AuthenticatedWithoutCustomerId_Returns403Forbidden()
    {
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");

        var response = await client.DeleteAsync($"/api/v1/customer/addresses/{addressId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAddress_OwnedAddress_ReturnsSuccess()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<DeleteCustomerAddressCommand>(c => c.CustomerId == customerId && c.AddressId == addressId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var response = await client.DeleteAsync($"/api/v1/customer/addresses/{addressId}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteAddress_MissingAddress_Returns404NotFound()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<DeleteCustomerAddressCommand>(c => c.CustomerId == customerId && c.AddressId == addressId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CustomerAddressErrors.NotFound));

        var response = await client.DeleteAsync($"/api/v1/customer/addresses/{addressId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAddress_CrossCustomerAddress_Returns404NotFound()
    {
        var customerId = Guid.NewGuid();
        var otherCustomerAddressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        _senderMock
            .Setup(s => s.Send(It.Is<DeleteCustomerAddressCommand>(c => c.CustomerId == customerId && c.AddressId == otherCustomerAddressId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(CustomerAddressErrors.NotFound));

        var response = await client.DeleteAsync($"/api/v1/customer/addresses/{otherCustomerAddressId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region PUT /api/v1/customer/addresses/{addressId}

    [Fact]
    public async Task UpdateAddress_Anonymous_Returns401Unauthorized()
    {
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        var request = new UpdateCustomerAddressRequest(
            "新姓名",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        var response = await client.PutAsJsonAsync($"/api/v1/customer/addresses/{addressId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateAddress_AuthenticatedWithoutCustomerId_Returns403Forbidden()
    {
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        var request = new UpdateCustomerAddressRequest(
            "新姓名",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        var response = await client.PutAsJsonAsync($"/api/v1/customer/addresses/{addressId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAddress_OwnedAddress_Returns200WithUpdatedResponse()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new UpdateCustomerAddressRequest(
            "新姓名",
            "0922222222",
            "us",
            "94105",
            "San Francisco",
            "Market St",
            "Suite 100");

        var updatedResponse = new CustomerAddressResponse(
            addressId,
            "新姓名",
            "0922222222",
            "US",
            "94105",
            "San Francisco",
            "Market St",
            "Suite 100");

        _senderMock
            .Setup(s => s.Send(It.Is<UpdateCustomerAddressCommand>(c =>
                c.CustomerId == customerId &&
                c.AddressId == addressId &&
                c.RecipientName == request.RecipientName &&
                c.Phone == request.Phone &&
                c.CountryCode == request.CountryCode &&
                c.PostalCode == request.PostalCode &&
                c.City == request.City &&
                c.AddressLine1 == request.AddressLine1 &&
                c.AddressLine2 == request.AddressLine2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(updatedResponse));

        var response = await client.PutAsJsonAsync($"/api/v1/customer/addresses/{addressId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(addressId);
        body.RecipientName.Should().Be("新姓名");
        body.CountryCode.Should().Be("US");
    }

    [Fact]
    public async Task UpdateAddress_InvalidDomainRequest_Returns400BadRequest()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new UpdateCustomerAddressRequest(
            "",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        _senderMock
            .Setup(s => s.Send(It.IsAny<UpdateCustomerAddressCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.InvalidRecipientName));

        var response = await client.PutAsJsonAsync($"/api/v1/customer/addresses/{addressId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAddress_MissingAddress_Returns404NotFound()
    {
        var customerId = Guid.NewGuid();
        var addressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new UpdateCustomerAddressRequest(
            "新姓名",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        _senderMock
            .Setup(s => s.Send(It.Is<UpdateCustomerAddressCommand>(c => c.CustomerId == customerId && c.AddressId == addressId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.NotFound));

        var response = await client.PutAsJsonAsync($"/api/v1/customer/addresses/{addressId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAddress_CrossCustomerAddress_Returns404NotFound()
    {
        var customerId = Guid.NewGuid();
        var otherCustomerAddressId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());

        var request = new UpdateCustomerAddressRequest(
            "新姓名",
            "0912345678",
            "TW",
            "100",
            "台北市",
            "忠孝西路",
            null);

        _senderMock
            .Setup(s => s.Send(It.Is<UpdateCustomerAddressCommand>(c => c.CustomerId == customerId && c.AddressId == otherCustomerAddressId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CustomerAddressResponse>(CustomerAddressErrors.NotFound));

        var response = await client.PutAsJsonAsync($"/api/v1/customer/addresses/{otherCustomerAddressId}", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}
