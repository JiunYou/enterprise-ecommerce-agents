using EnterpriseCommerce.Application.Marketing.Reviews.Queries.GetProductReviews;
using EnterpriseCommerce.Domain.Catalog;
using EnterpriseCommerce.Domain.Marketing;
using EnterpriseCommerce.Infrastructure.Persistence;
using EnterpriseCommerce.WebApi.Contracts.Marketing;
using EnterpriseCommerce.WebApi.IntegrationTests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace EnterpriseCommerce.WebApi.IntegrationTests.Marketing;

[Collection("IntegrationTests")]
public class ProductReviewMySqlAcceptanceTests : IAsyncLifetime
{
    private readonly MySqlFixture _mySqlFixture;
    private WebApplicationFactory<Program>? _factory;
    private DbContextOptions<EnterpriseCommerceDbContext> _dbContextOptions = null!;

    public ProductReviewMySqlAcceptanceTests(MySqlFixture mySqlFixture)
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
    public async Task ProductReview_RealMySql_EndToEnd_FullAcceptance()
    {
        // 1. 建立測試資料：上架商品、下架商品、顧客 A 與 顧客 B
        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        var activeProduct = Product.Create("Active Reviewable Product", "SKU-REV-001-" + Guid.NewGuid().ToString("N")[..8], 299m, "TWD").Value;
        var inactiveProduct = Product.Create("Inactive Product", "SKU-REV-INACT-" + Guid.NewGuid().ToString("N")[..8], 199m, "TWD").Value;
        inactiveProduct.Deactivate();

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddRangeAsync(activeProduct, inactiveProduct);
            await db.SaveChangesAsync();
        }

        var clientA = _factory!.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-a");
        clientA.DefaultRequestHeaders.Add("X-Test-User-Id", customerA.ToString());

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-b");
        clientB.DefaultRequestHeaders.Add("X-Test-User-Id", customerB.ToString());

        var anonymousClient = _factory.CreateClient();

        // 2. 顧客 A 提交 rating 5 與包含前後空白的留言
        var rawComment = "  這商品很棒！非常推薦！  ";
        var createRequestA = new CreateProductReviewRequest(5, rawComment);
        var responseA = await clientA.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", createRequestA);
        responseA.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. 使用全新的 DbContext 確認僅有 1 筆 ProductReview 且欄位完全正確
        await using (var verifyDb = CreateFreshDbContext())
        {
            var reviewsInDb = await verifyDb.ProductReviews
                .Where(r => r.ProductId == activeProduct.Id)
                .ToListAsync();

            reviewsInDb.Should().HaveCount(1);
            var persistedReview = reviewsInDb.Single();

            // 4. CustomerId 於內部正確
            persistedReview.CustomerId.Should().Be(customerA);
            // 5. ProductId 正確
            persistedReview.ProductId.Should().Be(activeProduct.Id);
            // 6. Rating 正確
            persistedReview.Rating.Should().Be(5);
            // 7. normalized Comment 正確 (Trim 後結果)
            persistedReview.Comment.Should().Be("這商品很棒！非常推薦！");
            // 8. CreatedAt 成功持久化保存
            persistedReview.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        }

        // 9. 公開 GET 端點可順利回傳 Review
        var publicGetResponse = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews");
        publicGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publicBody = await publicGetResponse.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(publicBody);
        var root = jsonDoc.RootElement;
        var itemsElement = root.GetProperty("items");
        itemsElement.GetArrayLength().Should().Be(1);
        var firstItem = itemsElement[0];

        // 10. 公開 GET 絕不暴露 CustomerId 或內部 Review Id
        firstItem.TryGetProperty("customerId", out _).Should().BeFalse("公開 DTO 不得暴露 customerId");
        firstItem.TryGetProperty("CustomerId", out _).Should().BeFalse("公開 DTO 不得暴露 CustomerId");
        firstItem.TryGetProperty("id", out _).Should().BeFalse("公開 DTO 不得暴露內部 review id");
        firstItem.TryGetProperty("Id", out _).Should().BeFalse("公開 DTO 不得暴露內部 review Id");
        firstItem.GetProperty("rating").GetInt32().Should().Be(5);
        firstItem.GetProperty("comment").GetString().Should().Be("這商品很棒！非常推薦！");

        // 11. 顧客 A 重複提交同商品評論 → 409 Conflict
        var duplicateResponse = await clientA.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", new CreateProductReviewRequest(4, "嘗試重複評論"));
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 12. 驗證實體資料表行數仍保持為 1 筆
        await using (var verifyDb = CreateFreshDbContext())
        {
            var totalInDb = await verifyDb.ProductReviews.CountAsync(r => r.ProductId == activeProduct.Id);
            totalInDb.Should().Be(1);
        }

        // 13. 顧客 B 可對同一商品提交獨立的評論（包含特殊 HTML 標籤字串，驗證 XSS 隔離為純字串）
        const string maliciousHtmlComment = "<script>alert('xss')</script><b>粗體文字</b>";
        var createRequestB = new CreateProductReviewRequest(3, maliciousHtmlComment);
        var responseB = await clientB.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", createRequestB);
        responseB.StatusCode.Should().Be(HttpStatusCode.OK);

        // 14. 公開 GET 總數成為 2 筆
        var publicGetResponse2 = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews");
        publicGetResponse2.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedReviews = await publicGetResponse2.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        pagedReviews.Should().NotBeNull();
        pagedReviews!.TotalCount.Should().Be(2);
        pagedReviews.Items.Should().HaveCount(2);

        // 15. 排序依據為 CreatedAt DESC, Id DESC（最新優先）
        pagedReviews.Items[0].Rating.Should().Be(3);
        pagedReviews.Items[0].Comment.Should().Be(maliciousHtmlComment); // 19. 純字串原樣保存與回傳，無 HTML 解構
        pagedReviews.Items[1].Rating.Should().Be(5);
        pagedReviews.Items[1].Comment.Should().Be("這商品很棒！非常推薦！");

        // 16. 下架商品無法接受新評論
        var inactivePostResponse = await clientA.PostAsJsonAsync($"/api/v1/products/{inactiveProduct.Id}/reviews", new CreateProductReviewRequest(5, "評論下架商品"));
        inactivePostResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 17. 下架商品的公開評論 GET 回傳 404 NotFound
        var inactiveGetResponse = await anonymousClient.GetAsync($"/api/v1/products/{inactiveProduct.Id}/reviews");
        inactiveGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 18. 不存在的商品在 POST 與 GET 均回傳 404 NotFound
        var nonExistentId = Guid.NewGuid();
        var notFoundPostResponse = await clientA.PostAsJsonAsync($"/api/v1/products/{nonExistentId}/reviews", new CreateProductReviewRequest(5, "評論不存在商品"));
        notFoundPostResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var notFoundGetResponse = await anonymousClient.GetAsync($"/api/v1/products/{nonExistentId}/reviews");
        notFoundGetResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 20. 分頁功能驗證：totalCount 正確、越界分頁回傳空列表
        var outOfRangeResponse = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews?page=99&pageSize=10");
        outOfRangeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var outOfRangePaged = await outOfRangeResponse.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        outOfRangePaged.Should().NotBeNull();
        outOfRangePaged!.Items.Should().BeEmpty();
        outOfRangePaged.TotalCount.Should().Be(2);
        outOfRangePaged.Page.Should().Be(99);
    }

    [Fact]
    public async Task ProductReviewSummary_RealMySql_EndToEnd_FullAcceptance()
    {
        // 1. Active Product 與對照商品、下架商品準備
        var activeProduct = Product.Create("Summary Active Product", "SKU-SUM-001-" + Guid.NewGuid().ToString("N")[..8], 500m, "TWD").Value;
        var otherProduct = Product.Create("Other Active Product", "SKU-SUM-002-" + Guid.NewGuid().ToString("N")[..8], 300m, "TWD").Value;
        var inactiveProduct = Product.Create("Summary Inactive Product", "SKU-SUM-INACT-" + Guid.NewGuid().ToString("N")[..8], 200m, "TWD").Value;
        inactiveProduct.Deactivate();

        await using (var db = CreateFreshDbContext())
        {
            await db.Products.AddRangeAsync(activeProduct, otherProduct, inactiveProduct);
            await db.SaveChangesAsync();
        }

        var anonymousClient = _factory!.CreateClient();

        // 2. No Reviews: totalCount=0, averageRating=null
        var initialResponse = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews");
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var initialData = await initialResponse.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        initialData.Should().NotBeNull();
        initialData!.TotalCount.Should().Be(0);
        initialData.AverageRating.Should().BeNull();
        initialData.Items.Should().BeEmpty();

        // 建立顧客客戶端輔助方法
        HttpClient CreateCustomerClient(Guid customerId)
        {
            var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthHandler.DefaultScheme, "token-" + customerId);
            c.DefaultRequestHeaders.Add("X-Test-User-Id", customerId.ToString());
            return c;
        }

        // 3. Customer A rating=5
        var clientA = CreateCustomerClient(Guid.NewGuid());
        var resA = await clientA.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", new CreateProductReviewRequest(5, "滿分好評"));
        resA.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Customer B rating=4
        var clientB = CreateCustomerClient(Guid.NewGuid());
        var resB = await clientB.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", new CreateProductReviewRequest(4, "四星滿意"));
        resB.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Customer C rating=3
        var clientC = CreateCustomerClient(Guid.NewGuid());
        var resC = await clientC.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", new CreateProductReviewRequest(3, "三星普通"));
        resC.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Public GET: totalCount=3, averageRating=4.0
        var getResponse3 = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews");
        getResponse3.StatusCode.Should().Be(HttpStatusCode.OK);
        var data3 = await getResponse3.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        data3.Should().NotBeNull();
        data3!.TotalCount.Should().Be(3);
        data3.AverageRating.Should().Be(4.0); // RED: 尚未在 DB 計算 averageRating 時將為 null

        // 7. Request pageSize=1: items count 1, totalCount=3, averageRating remains 4.0
        var getPagedResponse = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews?page=1&pageSize=1");
        getPagedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedData = await getPagedResponse.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        pagedData.Should().NotBeNull();
        pagedData!.Items.Should().HaveCount(1);
        pagedData.TotalCount.Should().Be(3);
        pagedData.AverageRating.Should().Be(4.0);

        // 8. Add another Review rating=1: totalCount=4, averageRating=3.25
        var clientD = CreateCustomerClient(Guid.NewGuid());
        var resD = await clientD.PostAsJsonAsync($"/api/v1/products/{activeProduct.Id}/reviews", new CreateProductReviewRequest(1, "一星差評"));
        resD.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse4 = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews");
        getResponse4.StatusCode.Should().Be(HttpStatusCode.OK);
        var data4 = await getResponse4.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        data4.Should().NotBeNull();
        data4!.TotalCount.Should().Be(4);
        data4.AverageRating.Should().Be(3.25);

        // 9. Average is scoped to Product: create another Product with a different rating; original Product average unchanged
        var clientE = CreateCustomerClient(Guid.NewGuid());
        var resOther = await clientE.PostAsJsonAsync($"/api/v1/products/{otherProduct.Id}/reviews", new CreateProductReviewRequest(2, "另一商品二星"));
        resOther.StatusCode.Should().Be(HttpStatusCode.OK);

        var otherResponse = await anonymousClient.GetAsync($"/api/v1/products/{otherProduct.Id}/reviews");
        otherResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var otherData = await otherResponse.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        otherData.Should().NotBeNull();
        otherData!.TotalCount.Should().Be(1);
        otherData.AverageRating.Should().Be(2.0);

        // 原商品的平均不受影響
        var originalAfterOther = await anonymousClient.GetAsync($"/api/v1/products/{activeProduct.Id}/reviews");
        originalAfterOther.StatusCode.Should().Be(HttpStatusCode.OK);
        var originalDataAfterOther = await originalAfterOther.Content.ReadFromJsonAsync<ProductReviewsResponse>();
        originalDataAfterOther.Should().NotBeNull();
        originalDataAfterOther!.TotalCount.Should().Be(4);
        originalDataAfterOther.AverageRating.Should().Be(3.25);

        // 10. Inactive Product still 404
        var inactiveResponse = await anonymousClient.GetAsync($"/api/v1/products/{inactiveProduct.Id}/reviews");
        inactiveResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 11. Missing Product still 404
        var missingResponse = await anonymousClient.GetAsync($"/api/v1/products/{Guid.NewGuid()}/reviews");
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 12. Public privacy contract unchanged
        var rawJson = await getResponse4.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(rawJson);
        var items = jsonDoc.RootElement.GetProperty("items");
        foreach (var element in items.EnumerateArray())
        {
            element.TryGetProperty("customerId", out _).Should().BeFalse();
            element.TryGetProperty("CustomerId", out _).Should().BeFalse();
            element.TryGetProperty("id", out _).Should().BeFalse();
            element.TryGetProperty("Id", out _).Should().BeFalse();
            element.TryGetProperty("reviewId", out _).Should().BeFalse();
            element.TryGetProperty("ReviewId", out _).Should().BeFalse();
        }
    }
}
