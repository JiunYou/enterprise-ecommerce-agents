using EnterpriseCommerce.Application.Customers.Addresses;
using EnterpriseCommerce.Domain.Customers;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Customers;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Customers;

[Collection("IntegrationTests")]
public class CustomerAddressMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public CustomerAddressMySqlAcceptanceTests(MySqlFixture mySqlFixture)
    {
        _mySqlFixture = mySqlFixture;
    }

    public async Task InitializeAsync()
    {
        _dbContextOptions = new DbContextOptionsBuilder<EnterpriseCommerceDbContext>()
            .UseMySql(_mySqlFixture.ConnectionString, ServerVersion.AutoDetect(_mySqlFixture.ConnectionString))
            .Options;

        await using (var dbContext = new EnterpriseCommerceDbContext(_dbContextOptions))
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Database", _mySqlFixture.ConnectionString);
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.DefaultScheme;
                    options.DefaultChallengeScheme = TestAuthHandler.DefaultScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.DefaultScheme, options => { });
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    private EnterpriseCommerceDbContext CreateFreshDbContext() => new(_dbContextOptions);

    [Fact]
    public async Task CustomerAddress_RealMySql_EndToEnd_Acceptance_And_CrossCustomerIsolation()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var clientA = _factory!.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-a");
        clientA.DefaultRequestHeaders.Add("X-Test-User-Id", customerA.ToString());

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-b");
        clientB.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        // 1. Customer A creates address A1 (含前後空白與小寫國碼)
        var createRequestA1 = new CreateCustomerAddressRequest(
            "  王小明  ",
            "  0912345678  ",
            "tw",
            " 100 ",
            " 台北市 ",
            " 中正區忠孝西路一段 ",
            " 3 樓之 1 ");

        var postResponseA1 = await clientA.PostAsJsonAsync("/api/v1/customer/addresses", createRequestA1);
        postResponseA1.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);

        var a1Json = await postResponseA1.Content.ReadAsStringAsync();
        using (var jsonDoc = JsonDocument.Parse(a1Json))
        {
            // 14. API never exposes CustomerId
            jsonDoc.RootElement.TryGetProperty("customerId", out _).Should().BeFalse("POST response must not expose customerId");
            jsonDoc.RootElement.TryGetProperty("CustomerId", out _).Should().BeFalse("POST response must not expose CustomerId");
        }

        var a1Response = JsonSerializer.Deserialize<CustomerAddressResponse>(a1Json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        a1Response.Should().NotBeNull();
        var a1Id = a1Response!.Id;
        a1Id.Should().NotBeEmpty();

        // 2. Fresh DbContext proves row persisted
        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1.Should().NotBeNull();
            dbA1!.CustomerId.Should().Be(customerA);

            // 3. normalized CountryCode is uppercase
            dbA1.CountryCode.Should().Be("TW");

            // 4. normalized strings persisted
            dbA1.RecipientName.Should().Be("王小明");
            dbA1.Phone.Should().Be("0912345678");
            dbA1.PostalCode.Should().Be("100");
            dbA1.City.Should().Be("台北市");
            dbA1.AddressLine1.Should().Be("中正區忠孝西路一段");
            dbA1.AddressLine2.Should().Be("3 樓之 1");
        }

        // 5. Customer A GET returns A1
        var getResponseA = await clientA.GetAsync("/api/v1/customer/addresses");
        getResponseA.StatusCode.Should().Be(HttpStatusCode.OK);
        var getJsonA = await getResponseA.Content.ReadAsStringAsync();
        using (var jsonDoc = JsonDocument.Parse(getJsonA))
        {
            // 14. API never exposes CustomerId in list
            foreach (var item in jsonDoc.RootElement.EnumerateArray())
            {
                item.TryGetProperty("customerId", out _).Should().BeFalse();
                item.TryGetProperty("CustomerId", out _).Should().BeFalse();
            }
        }
        var listA = JsonSerializer.Deserialize<List<CustomerAddressResponse>>(getJsonA, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        listA.Should().NotBeNull();
        listA!.Should().ContainSingle(a => a.Id == a1Id);

        // 6. Customer B GET does not return A1 (returns empty list)
        var getResponseB = await clientB.GetAsync("/api/v1/customer/addresses");
        getResponseB.StatusCode.Should().Be(HttpStatusCode.OK);
        var listB = await getResponseB.Content.ReadFromJsonAsync<List<CustomerAddressResponse>>();
        listB.Should().NotBeNull();
        listB!.Should().BeEmpty("Customer B must not see Customer A's address");

        // 7. Customer B DELETE A1 → 404 NotFound
        var deleteResponseB = await clientB.DeleteAsync($"/api/v1/customer/addresses/{a1Id}");
        deleteResponseB.StatusCode.Should().Be(HttpStatusCode.NotFound, "Cross-customer DELETE must return 404");

        // 8. Fresh DbContext proves A1 remains intact in database
        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1.Should().NotBeNull("A1 must not be deleted by Customer B");
        }

        // 9. Customer A creates A2 later
        await Task.Delay(50); // Ensure timestamp difference
        var createRequestA2 = new CreateCustomerAddressRequest(
            "王大同",
            "0987654321",
            "TW",
            "200",
            "基隆市",
            "仁愛區孝二路",
            null);
        var postResponseA2 = await clientA.PostAsJsonAsync("/api/v1/customer/addresses", createRequestA2);
        postResponseA2.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var a2Response = await postResponseA2.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a2Response.Should().NotBeNull();
        var a2Id = a2Response!.Id;

        // 10. A list is deterministic newest-first (A2 first, then A1)
        var getResponseA2 = await clientA.GetAsync("/api/v1/customer/addresses");
        getResponseA2.StatusCode.Should().Be(HttpStatusCode.OK);
        var orderedListA = await getResponseA2.Content.ReadFromJsonAsync<List<CustomerAddressResponse>>();
        orderedListA.Should().NotBeNull();
        orderedListA!.Should().HaveCount(2);
        orderedListA[0].Id.Should().Be(a2Id, "Newest address A2 should be first");
        orderedListA[1].Id.Should().Be(a1Id, "Older address A1 should be second");

        // 11. Customer A DELETE A1 succeeds
        var deleteResponseA = await clientA.DeleteAsync($"/api/v1/customer/addresses/{a1Id}");
        deleteResponseA.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        // 12. Fresh context proves A1 removed, A2 remains
        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1.Should().BeNull("A1 should be removed");

            var dbA2 = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a2Id);
            dbA2.Should().NotBeNull("A2 should still exist");
        }

        // 13. Invalid address creates no row in database
        var invalidRequest = new CreateCustomerAddressRequest(
            "", // Invalid name
            "0912345678",
            "TW",
            "100",
            "台北市",
            "地址一",
            null);
        var invalidResponse = await clientA.PostAsJsonAsync("/api/v1/customer/addresses", invalidRequest);
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using (var db = CreateFreshDbContext())
        {
            var invalidDbCount = await db.CustomerAddresses.CountAsync(a => a.AddressLine1 == "地址一");
            invalidDbCount.Should().Be(0, "Invalid address must not create a row");
        }

        // 15. No cross-customer PII leakage: Customer B can never see A2
        var getResponseBFinal = await clientB.GetAsync("/api/v1/customer/addresses");
        var listBFinal = await getResponseBFinal.Content.ReadFromJsonAsync<List<CustomerAddressResponse>>();
        listBFinal.Should().BeEmpty();
    }

    [Fact]
    public async Task CustomerAddress_Update_RealMySql_EndToEnd_Acceptance_And_CrossCustomerIsolation()
    {
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var clientA = _factory!.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-a");
        clientA.DefaultRequestHeaders.Add("X-Test-User-Id", customerA.ToString());

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-b");
        clientB.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        // 1. Customer A creates address A1
        var createRequestA1 = new CreateCustomerAddressRequest(
            "  王小明  ",
            "  0912345678  ",
            "tw",
            " 100 ",
            " 台北市 ",
            " 中正區忠孝西路一段 ",
            " 3 樓之 1 ");

        var postResponseA1 = await clientA.PostAsJsonAsync("/api/v1/customer/addresses", createRequestA1);
        postResponseA1.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var a1Response = await postResponseA1.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a1Response.Should().NotBeNull();
        var a1Id = a1Response!.Id;

        // 2. Capture Id, CustomerId, CreatedAt and row count
        Guid originalId;
        Guid originalCustomerId;
        DateTimeOffset originalCreatedAt;
        int countWithA1;

        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1.Should().NotBeNull();
            originalId = dbA1!.Id;
            originalCustomerId = dbA1.CustomerId;
            originalCreatedAt = dbA1.CreatedAt;
            countWithA1 = await db.CustomerAddresses.CountAsync();
        }

        // 建立 A2 以便後續檢驗清單排序未因 A1 更新而改變
        await Task.Delay(50);
        var createRequestA2 = new CreateCustomerAddressRequest(
            "王大同",
            "0987654321",
            "TW",
            "200",
            "基隆市",
            "仁愛區孝二路",
            null);
        var postResponseA2 = await clientA.PostAsJsonAsync("/api/v1/customer/addresses", createRequestA2);
        postResponseA2.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created);
        var a2Response = await postResponseA2.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        var a2Id = a2Response!.Id;

        int totalCountBeforeUpdate;
        await using (var db = CreateFreshDbContext())
        {
            totalCountBeforeUpdate = await db.CustomerAddresses.CountAsync();
            totalCountBeforeUpdate.Should().Be(countWithA1 + 1);
        }

        // 3. Customer A PUT updates every mutable field
        var updateRequestA1 = new UpdateCustomerAddressRequest(
            "  陳大文  ",
            "  0988776655  ",
            "us",
            " 94105 ",
            " 舊金山 ",
            " 市場街 100 號 ",
            "    "); // Whitespace to be normalized to null

        var putResponse = await clientA.PutAsJsonAsync($"/api/v1/customer/addresses/{a1Id}", updateRequestA1);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 18. API response does not expose CustomerId
        var putJson = await putResponse.Content.ReadAsStringAsync();
        using (var jsonDoc = JsonDocument.Parse(putJson))
        {
            jsonDoc.RootElement.TryGetProperty("customerId", out _).Should().BeFalse("PUT response must not expose customerId");
            jsonDoc.RootElement.TryGetProperty("CustomerId", out _).Should().BeFalse("PUT response must not expose CustomerId");
        }

        var putResult = JsonSerializer.Deserialize<CustomerAddressResponse>(putJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        putResult.Should().NotBeNull();
        putResult!.Id.Should().Be(a1Id);
        putResult.RecipientName.Should().Be("陳大文");
        putResult.Phone.Should().Be("0988776655");
        putResult.CountryCode.Should().Be("US");
        putResult.PostalCode.Should().Be("94105");
        putResult.City.Should().Be("舊金山");
        putResult.AddressLine1.Should().Be("市場街 100 號");
        putResult.AddressLine2.Should().BeNull();

        // 4. Fresh DbContext proves replacement values persisted
        // 5. CountryCode normalized uppercase
        // 6. AddressLine2 whitespace became null
        // 7. Id unchanged
        // 8. CustomerId unchanged
        // 9. CreatedAt unchanged
        // 10. physical row count unchanged
        await using (var db = CreateFreshDbContext())
        {
            var dbA1Updated = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1Updated.Should().NotBeNull();
            dbA1Updated!.Id.Should().Be(originalId);
            dbA1Updated.CustomerId.Should().Be(originalCustomerId);
            dbA1Updated.CreatedAt.Should().Be(originalCreatedAt);
            dbA1Updated.RecipientName.Should().Be("陳大文");
            dbA1Updated.Phone.Should().Be("0988776655");
            dbA1Updated.CountryCode.Should().Be("US");
            dbA1Updated.PostalCode.Should().Be("94105");
            dbA1Updated.City.Should().Be("舊金山");
            dbA1Updated.AddressLine1.Should().Be("市場街 100 號");
            dbA1Updated.AddressLine2.Should().BeNull();

            var currentCount = await db.CustomerAddresses.CountAsync();
            currentCount.Should().Be(totalCountBeforeUpdate, "Row count must remain unchanged after update");
        }

        // 11. Customer B attempts PUT on A1 -> 404
        var hackRequest = new UpdateCustomerAddressRequest(
            "惡意竄改者",
            "0900111222",
            "TW",
            "100",
            "台北市",
            "攻擊地址",
            null);
        var crossPutResponse = await clientB.PutAsJsonAsync($"/api/v1/customer/addresses/{a1Id}", hackRequest);
        crossPutResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 12. Fresh DbContext proves A1 unchanged after B attempt
        await using (var db = CreateFreshDbContext())
        {
            var dbA1AfterHack = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1AfterHack.Should().NotBeNull();
            dbA1AfterHack!.RecipientName.Should().Be("陳大文");
            dbA1AfterHack.Phone.Should().Be("0988776655");
            dbA1AfterHack.AddressLine1.Should().Be("市場街 100 號");
        }

        // 13. missing AddressId -> 404
        var nonExistentAddressId = Guid.NewGuid();
        var missingPutResponse = await clientA.PutAsJsonAsync($"/api/v1/customer/addresses/{nonExistentAddressId}", updateRequestA1);
        missingPutResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 14. invalid update -> 400
        var invalidUpdateRequest = new UpdateCustomerAddressRequest(
            "", // Invalid name
            "0988776655",
            "US",
            "94105",
            "舊金山",
            "市場街",
            null);
        var invalidPutResponse = await clientA.PutAsJsonAsync($"/api/v1/customer/addresses/{a1Id}", invalidUpdateRequest);
        invalidPutResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 15. fresh DbContext proves invalid update caused zero mutation
        await using (var db = CreateFreshDbContext())
        {
            var dbA1AfterInvalid = await db.CustomerAddresses.FirstOrDefaultAsync(a => a.Id == a1Id);
            dbA1AfterInvalid.Should().NotBeNull();
            dbA1AfterInvalid!.RecipientName.Should().Be("陳大文");
            dbA1AfterInvalid.Phone.Should().Be("0988776655");
        }

        // 16. list endpoint returns updated values
        // 17. list ordering still follows original CreatedAt/Id semantics (A2 first, A1 second)
        var getListResponse = await clientA.GetAsync("/api/v1/customer/addresses");
        getListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listJson = await getListResponse.Content.ReadAsStringAsync();
        using (var jsonDoc = JsonDocument.Parse(listJson))
        {
            foreach (var item in jsonDoc.RootElement.EnumerateArray())
            {
                item.TryGetProperty("customerId", out _).Should().BeFalse();
                item.TryGetProperty("CustomerId", out _).Should().BeFalse();
            }
        }
        var listA = JsonSerializer.Deserialize<List<CustomerAddressResponse>>(listJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        listA.Should().NotBeNull();
        listA!.Should().HaveCount(2);
        listA[0].Id.Should().Be(a2Id, "Newer A2 must still be listed first");
        listA[1].Id.Should().Be(a1Id, "Updated A1 must preserve original ordering");
        listA[1].RecipientName.Should().Be("陳大文");
        listA[1].Phone.Should().Be("0988776655");
        listA[1].CountryCode.Should().Be("US");
        listA[1].City.Should().Be("舊金山");
        listA[1].AddressLine1.Should().Be("市場街 100 號");
        listA[1].AddressLine2.Should().BeNull();
    }
}
