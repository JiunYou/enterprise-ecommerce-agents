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

    [Fact]
    public async Task CustomerDefaultAddress_RealMySql_Acceptance_And_Concurrency_Invariant()
    {
        var customer1 = Guid.NewGuid();
        var customer2 = Guid.NewGuid();

        var client1 = _factory!.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-c1");
        client1.DefaultRequestHeaders.Add("X-Test-User-Id", customer1.ToString());

        var client2 = _factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-c2");
        client2.DefaultRequestHeaders.Add("X-Test-User-Id", customer2.ToString());

        // 建立 Customer 1 的地址 A1 與 A2
        var postA1 = await client1.PostAsJsonAsync("/api/v1/customer/addresses", new CreateCustomerAddressRequest(
            "地址A1", "0911111111", "TW", "100", "台北市", "忠孝東路一段", null));
        postA1.StatusCode.Should().Be(HttpStatusCode.OK);
        var a1Dto = await postA1.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a1Dto.Should().NotBeNull();
        a1Dto!.IsDefault.Should().BeFalse("新建地址預設狀態必須為 false");
        var a1Id = a1Dto.Id;

        var postA2 = await client1.PostAsJsonAsync("/api/v1/customer/addresses", new CreateCustomerAddressRequest(
            "地址A2", "0922222222", "TW", "100", "台北市", "忠孝東路二段", null));
        postA2.StatusCode.Should().Be(HttpStatusCode.OK);
        var a2Dto = await postA2.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a2Dto.Should().NotBeNull();
        a2Dto!.IsDefault.Should().BeFalse("新建地址預設狀態必須為 false");
        var a2Id = a2Dto.Id;

        // 建立 Customer 2 的地址 B1
        var postB1 = await client2.PostAsJsonAsync("/api/v1/customer/addresses", new CreateCustomerAddressRequest(
            "地址B1", "0933333333", "TW", "100", "台北市", "忠孝東路三段", null));
        postB1.StatusCode.Should().Be(HttpStatusCode.OK);
        var b1Dto = await postB1.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        b1Dto.Should().NotBeNull();
        var b1Id = b1Dto!.Id;

        // ==========================================
        // A. Basic set-default
        // ==========================================
        // 初始狀態: A1=false, A2=false
        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstAsync(a => a.Id == a1Id);
            var dbA2 = await db.CustomerAddresses.FirstAsync(a => a.Id == a2Id);
            dbA1.IsDefault.Should().BeFalse();
            dbA2.IsDefault.Should().BeFalse();
        }

        // Set A1 as default -> A1=true, A2=false
        var setDefaultA1Res = await client1.PutAsync($"/api/v1/customer/addresses/{a1Id}/default", null);
        setDefaultA1Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var a1UpdatedDto = await setDefaultA1Res.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a1UpdatedDto.Should().NotBeNull();
        a1UpdatedDto!.IsDefault.Should().BeTrue();

        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstAsync(a => a.Id == a1Id);
            var dbA2 = await db.CustomerAddresses.FirstAsync(a => a.Id == a2Id);
            dbA1.IsDefault.Should().BeTrue();
            dbA2.IsDefault.Should().BeFalse();
        }

        // Set A2 as default -> A1=false, A2=true
        var setDefaultA2Res = await client1.PutAsync($"/api/v1/customer/addresses/{a2Id}/default", null);
        setDefaultA2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var a2UpdatedDto = await setDefaultA2Res.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a2UpdatedDto.Should().NotBeNull();
        a2UpdatedDto!.IsDefault.Should().BeTrue();

        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstAsync(a => a.Id == a1Id);
            var dbA2 = await db.CustomerAddresses.FirstAsync(a => a.Id == a2Id);
            dbA1.IsDefault.Should().BeFalse();
            dbA2.IsDefault.Should().BeTrue();
        }

        // ==========================================
        // B. Idempotence
        // ==========================================
        // Set A2 again -> A1=false, A2=true
        var setDefaultA2AgainRes = await client1.PutAsync($"/api/v1/customer/addresses/{a2Id}/default", null);
        setDefaultA2AgainRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var a2AgainDto = await setDefaultA2AgainRes.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        a2AgainDto.Should().NotBeNull();
        a2AgainDto!.IsDefault.Should().BeTrue();

        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstAsync(a => a.Id == a1Id);
            var dbA2 = await db.CustomerAddresses.FirstAsync(a => a.Id == a2Id);
            dbA1.IsDefault.Should().BeFalse();
            dbA2.IsDefault.Should().BeTrue();
        }

        // ==========================================
        // C. Cross-customer isolation
        // ==========================================
        // Customer 1 嘗試設定 Customer 2 的 B1 地址為預設 -> 404，且雙方狀態均未被修改
        var crossCustomerRes = await client1.PutAsync($"/api/v1/customer/addresses/{b1Id}/default", null);
        crossCustomerRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using (var db = CreateFreshDbContext())
        {
            var dbA1 = await db.CustomerAddresses.FirstAsync(a => a.Id == a1Id);
            var dbA2 = await db.CustomerAddresses.FirstAsync(a => a.Id == a2Id);
            var dbB1 = await db.CustomerAddresses.FirstAsync(a => a.Id == b1Id);
            dbA1.IsDefault.Should().BeFalse();
            dbA2.IsDefault.Should().BeTrue("Customer 1 預設地址保持 A2");
            dbB1.IsDefault.Should().BeFalse("Customer 2 的地址未受任何影響");
        }

        // ==========================================
        // D. Update preservation
        // ==========================================
        // 更新 A2 地址內容 -> A2 依然為 IsDefault=true
        var updateA2Res = await client1.PutAsJsonAsync($"/api/v1/customer/addresses/{a2Id}", new UpdateCustomerAddressRequest(
            "地址A2改名", "0922222222", "TW", "100", "台北市", "忠孝東路二段變更", null));
        updateA2Res.StatusCode.Should().Be(HttpStatusCode.OK);
        var updateA2Dto = await updateA2Res.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        updateA2Dto.Should().NotBeNull();
        updateA2Dto!.IsDefault.Should().BeTrue("更新地址欄位時必須保留原有的 IsDefault 狀態");

        await using (var db = CreateFreshDbContext())
        {
            var dbA2 = await db.CustomerAddresses.FirstAsync(a => a.Id == a2Id);
            dbA2.RecipientName.Should().Be("地址A2改名");
            dbA2.IsDefault.Should().BeTrue();
        }

        // ==========================================
        // E. Delete default
        // ==========================================
        // 刪除目前為預設的 A2 地址 -> 剩餘地址 DefaultAddressCount == 0（不自動遞補）
        var deleteA2Res = await client1.DeleteAsync($"/api/v1/customer/addresses/{a2Id}");
        deleteA2Res.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var db = CreateFreshDbContext())
        {
            var dbAddressesA = await db.CustomerAddresses.Where(a => a.CustomerId == customer1).ToListAsync();
            dbAddressesA.Should().HaveCount(1);
            dbAddressesA[0].Id.Should().Be(a1Id);
            dbAddressesA[0].IsDefault.Should().BeFalse("刪除預設地址後，其餘地址不可自動遞補，預設地址數量應為 0");
        }

        // ==========================================
        // F. Concurrent Set Default
        // ==========================================
        // 建立兩個新地址 C1 與 C2
        var postC1 = await client1.PostAsJsonAsync("/api/v1/customer/addresses", new CreateCustomerAddressRequest(
            "並行測試C1", "0911223344", "TW", "100", "台北市", "仁愛路一段", null));
        var c1Dto = await postC1.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        var c1Id = c1Dto!.Id;

        var postC2 = await client1.PostAsJsonAsync("/api/v1/customer/addresses", new CreateCustomerAddressRequest(
            "並行測試C2", "0911223355", "TW", "100", "台北市", "仁愛路二段", null));
        var c2Dto = await postC2.Content.ReadFromJsonAsync<CustomerAddressResponse>();
        var c2Id = c2Dto!.Id;

        // 建立獨立的 HttpClient 模擬兩個同時並行的 Set-Default 請求
        var clientThread1 = _factory.CreateClient();
        clientThread1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-c1-t1");
        clientThread1.DefaultRequestHeaders.Add("X-Test-User-Id", customer1.ToString());

        var clientThread2 = _factory.CreateClient();
        clientThread2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-c1-t2");
        clientThread2.DefaultRequestHeaders.Add("X-Test-User-Id", customer1.ToString());

        var task1 = clientThread1.PutAsync($"/api/v1/customer/addresses/{c1Id}/default", null);
        var task2 = clientThread2.PutAsync($"/api/v1/customer/addresses/{c2Id}/default", null);

        var responses = await Task.WhenAll(task1, task2);

        responses[0].StatusCode.Should().Be(HttpStatusCode.OK);
        responses[1].StatusCode.Should().Be(HttpStatusCode.OK);

        // 驗證核心不變量：在並行 Set-Default 之後，該客戶的預設地址數量必須恰好為 1
        await using (var db = CreateFreshDbContext())
        {
            var defaultCount = await db.CustomerAddresses.CountAsync(a => a.CustomerId == customer1 && a.IsDefault);
            defaultCount.Should().Be(1, "並行執行 SetDefault 後，客戶的預設地址數量必須恰好為 1");
        }
    }
}
