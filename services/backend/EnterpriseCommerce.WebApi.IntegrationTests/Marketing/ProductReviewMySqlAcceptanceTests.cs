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
}
