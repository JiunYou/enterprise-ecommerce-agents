using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Web;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayRefundContractSimulationTests
{
    private const string MerchantId = "3002607";
    private const string HashKey = "pwFHCqoQZGm3CC6v";
    private const string HashIv = "EkRm7iFT261dpevs";
    private const string CreditCheckCode = "888999";

    [Fact]
    public async Task ExecuteRefundAsync_WhenStateIsAuthorized_ExecutesDoActionN_WithCorrectContract()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132111111",
                "status": "已授權",
                "close_data": []
            }
        }
        """);
        server.SetDoActionResponse("N", "RtnCode=1&RtnMsg=Success&TradeNo=2000132111111");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var paymentAttemptId = new PaymentAttemptId(Guid.NewGuid());
            var request = new RefundExecutionRequest(
                paymentAttemptId,
                ProviderTransactionId: "2000132111111",
                ProviderAuthorizationReference: "gwsr12345678",
                Amount: 500m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.RefundCompleted);
            result.ProviderTransactionId.Should().Be("2000132111111");

            // 驗證呼叫次數與順序
            var requests = server.GetRecordedRequests();
            requests.Should().HaveCount(2);
            var queryReq = requests[0];
            var actionReq = requests[1];

            // 1. QueryTrade Contract 驗證
            queryReq.Method.Should().Be("POST");
            queryReq.Path.Should().Be("/QueryTrade/V2");
            queryReq.ContentType.Should().StartWith("application/x-www-form-urlencoded");
            queryReq.Form["MerchantID"].Should().Be(MerchantId);
            queryReq.Form["CreditRefundId"].Should().Be("gwsr12345678");
            queryReq.Form["CreditAmount"].Should().Be("500");
            queryReq.Form["CreditCheckCode"].Should().Be(CreditCheckCode);
            queryReq.IsCheckMacValid.Should().BeTrue();
            queryReq.Form.Should().NotContainKey("TradeNo");
            queryReq.Form.Should().NotContainKey("HashKey");
            queryReq.Form.Should().NotContainKey("HashIV");

            // 2. DoAction N Contract 驗證
            actionReq.Method.Should().Be("POST");
            actionReq.Path.Should().Be("/DoAction");
            actionReq.ContentType.Should().StartWith("application/x-www-form-urlencoded");
            actionReq.Form["MerchantID"].Should().Be(MerchantId);
            actionReq.Form["MerchantTradeNo"].Should().Be(ECPayMerchantTradeNo.FromPaymentAttemptId(paymentAttemptId));
            actionReq.Form["TradeNo"].Should().Be("2000132111111");
            actionReq.Form["Action"].Should().Be("N");
            actionReq.Form["TotalAmount"].Should().Be("500");
            actionReq.IsCheckMacValid.Should().BeTrue();
            actionReq.Form.Should().NotContainKey("CreditRefundId");
            actionReq.Form.Should().NotContainKey("CreditCheckCode");

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountDoActionCalls("N").Should().Be(1);
            server.CountDoActionCalls("E").Should().Be(0);
            server.CountDoActionCalls("R").Should().Be(0);
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenStateIsToBeCaptured_ExecutesDoActionE_ThenDoActionN()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132222222",
                "status": "已授權",
                "close_data": [
                    { "status": "要關帳", "amount": 1200 }
                ]
            }
        }
        """);
        server.SetDoActionResponse("E", "RtnCode=1&RtnMsg=Success");
        server.SetDoActionResponse("N", "RtnCode=1&RtnMsg=Success");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var paymentAttemptId = new PaymentAttemptId(Guid.NewGuid());
            var request = new RefundExecutionRequest(
                paymentAttemptId,
                ProviderTransactionId: "2000132222222",
                ProviderAuthorizationReference: "gwsr87654321",
                Amount: 1200m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.RefundCompleted);

            var requests = server.GetRecordedRequests();
            requests.Should().HaveCount(3);
            requests[0].Path.Should().Be("/QueryTrade/V2");
            requests[1].Form["Action"].Should().Be("E");
            requests[2].Form["Action"].Should().Be("N");

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountDoActionCalls("E").Should().Be(1);
            server.CountDoActionCalls("N").Should().Be(1);
            server.CountDoActionCalls("R").Should().Be(0);
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenStateIsToBeCaptured_AndEActionFails_HaltsImmediatelyWithoutN()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132333333",
                "status": "已授權",
                "close_data": [
                    { "status": "要關帳", "amount": 800 }
                ]
            }
        }
        """);
        // E 動作明確失敗
        server.SetDoActionResponse("E", "RtnCode=10200073&RtnMsg=ActionFailed");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132333333",
                ProviderAuthorizationReference: "gwsr33333333",
                Amount: 800m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.ManualProviderResolutionRequired);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountDoActionCalls("E").Should().Be(1);
            server.CountDoActionCalls("N").Should().Be(0); // 絕對不執行 N
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenStateIsToBeCaptured_AndEActionUncertain_HaltsImmediatelyWithoutN()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132444444",
                "status": "已授權",
                "close_data": [
                    { "status": "要關帳", "amount": 950 }
                ]
            }
        }
        """);
        // E 動作回傳 HTTP 500
        server.SetCustomResponse("/DoAction", HttpStatusCode.InternalServerError, "Internal Server Error");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132444444",
                ProviderAuthorizationReference: "gwsr44444444",
                Amount: 950m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.Unresolved);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountDoActionCalls("E").Should().Be(1);
            server.CountDoActionCalls("N").Should().Be(0); // 絕對不執行 N
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenStateIsCaptured_ExecutesDoActionR()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132555555",
                "status": "已關帳",
                "close_data": []
            }
        }
        """);
        server.SetDoActionResponse("R", "RtnCode=1&RtnMsg=Success");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132555555",
                ProviderAuthorizationReference: "gwsr55555555",
                Amount: 2000m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.RefundCompleted);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountDoActionCalls("R").Should().Be(1);
            server.CountDoActionCalls("E").Should().Be(0);
            server.CountDoActionCalls("N").Should().Be(0);
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenStateAlreadyCancelled_ExecutesZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132666666",
                "status": "已取消",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132666666",
                ProviderAuthorizationReference: "gwsr66666666",
                Amount: 150m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.AlreadyRefunded);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 零 DoAction
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenQueryFailsWithNonEmptyRtnMsg_ExecutesZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "查無授權資料",
            "RtnValue": null
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132777777",
                ProviderAuthorizationReference: "gwsr77777777",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.Unresolved);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 零 DoAction
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenQueryReturnsHttp403_ExecutesZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetCustomResponse("/QueryTrade/V2", HttpStatusCode.Forbidden, "Forbidden");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132888888",
                ProviderAuthorizationReference: "gwsr88888888",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.Unresolved);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 零 DoAction
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenQueryReturnsHttp500_ExecutesZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetCustomResponse("/QueryTrade/V2", HttpStatusCode.InternalServerError, "Error");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132999999",
                ProviderAuthorizationReference: "gwsr99999999",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.Unresolved);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 零 DoAction
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenInTaiwanBlackoutWindow_ExecutesZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132101010",
                "status": "已授權",
                "close_data": []
            }
        }
        """);

        // 模擬台灣時間 20:20:00 (UTC 12:20:00) 落在禁運時段內
        var blackoutUtc = new DateTimeOffset(2026, 9, 7, 12, 20, 0, TimeSpan.Zero);
        var fakeClock = new TestTimeProvider(blackoutUtc);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, fakeClock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132101010",
                ProviderAuthorizationReference: "gwsr10101010",
                Amount: 600m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.ExecutionTemporarilyBlocked);

            // QueryTrade 可被執行以確定狀態，但 DoAction 次數必須為 0
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task ReconcileRefundAsync_WhenProviderProvesCancelled_ReturnsAlreadyRefunded_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132111222",
                "status": "已取消",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundReconciliationRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111222",
                ProviderAuthorizationReference: "gwsr11122233",
                Amount: 700m,
                Currency: "TWD");

            var result = await provider.ReconcileRefundAsync(request);

            result.Outcome.Should().Be(RefundReconciliationOutcome.AlreadyRefunded);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 對帳絕不發起 DoAction
        }
    }

    [Fact]
    public async Task ReconcileRefundAsync_WhenProviderStillCaptured_ReturnsManualResolutionRequired_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132111333",
                "status": "已關帳",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundReconciliationRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111333",
                ProviderAuthorizationReference: "gwsr11133344",
                Amount: 850m,
                Currency: "TWD");

            var result = await provider.ReconcileRefundAsync(request);

            result.Outcome.Should().Be(RefundReconciliationOutcome.ManualProviderResolutionRequired);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 對帳絕不發起 DoAction
        }
    }

    [Fact]
    public async Task ReconcileRefundAsync_WhenQueryFails_ReturnsUnresolved_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetCustomResponse("/QueryTrade/V2", HttpStatusCode.InternalServerError, "Fail");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundReconciliationRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111444",
                ProviderAuthorizationReference: "gwsr11144455",
                Amount: 850m,
                Currency: "TWD");

            var result = await provider.ReconcileRefundAsync(request);

            result.Outcome.Should().Be(RefundReconciliationOutcome.Unresolved);

            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("JPY")]
    public async Task ExecuteRefundAsync_WhenCurrencyNotTwd_FailsClosedWithoutHttp(string unsupportedCurrency)
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111555",
                ProviderAuthorizationReference: "gwsr11155566",
                Amount: 500m,
                Currency: unsupportedCurrency);

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.ManualProviderResolutionRequired);
            server.RecordedRequests.Should().BeEmpty(); // 零 HTTP
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(100.5)]
    [InlineData(99.99)]
    public async Task ExecuteRefundAsync_WhenAmountInvalid_FailsClosedWithoutHttp(decimal invalidAmount)
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111666",
                ProviderAuthorizationReference: "gwsr11166677",
                Amount: invalidAmount,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.ManualProviderResolutionRequired);
            server.RecordedRequests.Should().BeEmpty(); // 零 HTTP
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-special-char!@#")]
    public async Task ExecuteRefundAsync_WhenProviderAuthRefInvalid_FailsClosedWithoutHttp(string? invalidRef)
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111777",
                ProviderAuthorizationReference: invalidRef,
                Amount: 500m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.ManualProviderResolutionRequired);
            server.RecordedRequests.Should().BeEmpty(); // 零 HTTP
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_WhenMissingCreditCheckCode_ThrowsBeforeHttp()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        var (provider, httpClient) = server.CreateProvider(MerchantId, creditCheckCode: "");
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111888",
                ProviderAuthorizationReference: "gwsr11188899",
                Amount: 500m,
                Currency: "TWD");

            var act = () => provider.ExecuteRefundAsync(request);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*CreditCheckCode*");
            server.RecordedRequests.Should().BeEmpty(); // 零 HTTP
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_TaiwanBlackoutBoundaries_201459_AllowsDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132111999",
                "status": "已授權",
                "close_data": []
            }
        }
        """);
        server.SetDoActionResponse("N", "RtnCode=1&RtnMsg=Success");

        // 2026-09-07 20:14:59 Taiwan time -> UTC 12:14:59
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 14, 59, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132111999",
                ProviderAuthorizationReference: "gwsr11199900",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.RefundCompleted);
            server.CountDoActionCalls("N").Should().Be(1);
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_TaiwanBlackoutBoundaries_203000_AllowsDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132112000",
                "status": "已授權",
                "close_data": []
            }
        }
        """);
        server.SetDoActionResponse("N", "RtnCode=1&RtnMsg=Success");

        // 2026-09-07 20:30:00 Taiwan time -> UTC 12:30:00
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 30, 0, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132112000",
                ProviderAuthorizationReference: "gwsr11200011",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.RefundCompleted);
            server.CountDoActionCalls("N").Should().Be(1);
        }
    }

    [Fact]
    public async Task ExecuteRefundAsync_TaiwanBlackoutBoundaries_202959_BlocksDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132112001",
                "status": "已授權",
                "close_data": []
            }
        }
        """);

        // 2026-09-07 20:29:59 Taiwan time -> UTC 12:29:59
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 29, 59, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132112001",
                ProviderAuthorizationReference: "gwsr11200122",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.ExecuteRefundAsync(request);

            result.Outcome.Should().Be(RefundExecutionOutcome.ExecutionTemporarilyBlocked);
            server.CountDoActionCalls("N").Should().Be(0);
        }
    }

    #region CheckExecutionReadinessAsync Tests (Pre-Intent Readiness Gate)

    [Fact]
    public async Task Readiness_Authorized_ReturnsReady_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300001",
                "status": "已授權",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300001",
                ProviderAuthorizationReference: "gwsr30000001",
                Amount: 500m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Ready);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 絕不動錢
        }
    }

    [Fact]
    public async Task Readiness_ToBeCaptured_ReturnsReady_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300002",
                "status": "已授權",
                "close_data": [
                    { "status": "要關帳", "amount": 800 }
                ]
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300002",
                ProviderAuthorizationReference: "gwsr30000002",
                Amount: 800m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Ready);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 絕不動錢
        }
    }

    [Fact]
    public async Task Readiness_Captured_ReturnsReady_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300003",
                "status": "已關帳",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300003",
                ProviderAuthorizationReference: "gwsr30000003",
                Amount: 1500m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Ready);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0); // 絕不動錢
        }
    }

    [Fact]
    public async Task Readiness_AlreadyRefunded_ReturnsAlreadyRefunded_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300004",
                "status": "已取消",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300004",
                ProviderAuthorizationReference: "gwsr30000004",
                Amount: 200m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.AlreadyRefunded);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Unauthorized_ReturnsManual_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300005",
                "status": "未授權",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300005",
                ProviderAuthorizationReference: "gwsr30000005",
                Amount: 200m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.ManualProviderResolutionRequired);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_ActionCancelled_ReturnsManual_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300006",
                "status": "操作取消",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300006",
                ProviderAuthorizationReference: "gwsr30000006",
                Amount: 200m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.ManualProviderResolutionRequired);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_UnknownStatus_ReturnsUnresolved_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300007",
                "status": "未知的怪異狀態",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300007",
                ProviderAuthorizationReference: "gwsr30000007",
                Amount: 200m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Unresolved);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Http403_ReturnsUnresolved_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetCustomResponse("/QueryTrade/V2", HttpStatusCode.Forbidden, "Forbidden");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300008",
                ProviderAuthorizationReference: "gwsr30000008",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Unresolved);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Http500_ReturnsUnresolved_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetCustomResponse("/QueryTrade/V2", HttpStatusCode.InternalServerError, "Error");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300009",
                ProviderAuthorizationReference: "gwsr30000009",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Unresolved);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_MalformedJson_ReturnsUnresolved_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("{ not a json }");

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300010",
                ProviderAuthorizationReference: "gwsr30000010",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Unresolved);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_NonEmptyRtnMsg_ReturnsUnresolved_AndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "查無資料",
            "RtnValue": null
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300011",
                ProviderAuthorizationReference: "gwsr30000011",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Unresolved);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Blackout_201459_AllowsQuery()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300012",
                "status": "已授權",
                "close_data": []
            }
        }
        """);

        // 2026-09-07 20:14:59 Taiwan time -> UTC 12:14:59
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 14, 59, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300012",
                ProviderAuthorizationReference: "gwsr30000012",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Ready);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Blackout_201500_BlocksWithZeroQueryAndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        // 2026-09-07 20:15:00 Taiwan time -> UTC 12:15:00
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 15, 0, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300013",
                ProviderAuthorizationReference: "gwsr30000013",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.ExecutionTemporarilyBlocked);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(0); // Section 12: Blackout 前置阻斷，Query=0
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Blackout_202959_BlocksWithZeroQueryAndZeroDoAction()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        // 2026-09-07 20:29:59 Taiwan time -> UTC 12:29:59
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 29, 59, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300014",
                ProviderAuthorizationReference: "gwsr30000014",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.ExecutionTemporarilyBlocked);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(0); // Section 12: Blackout 前置阻斷，Query=0
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_Blackout_203000_AllowsQuery()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300015",
                "status": "已授權",
                "close_data": []
            }
        }
        """);

        // 2026-09-07 20:30:00 Taiwan time -> UTC 12:30:00
        var clock = new TestTimeProvider(new DateTimeOffset(2026, 9, 7, 12, 30, 0, TimeSpan.Zero));
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode, clock);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300015",
                ProviderAuthorizationReference: "gwsr30000015",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.Ready);
            server.CountCallsByPath("/QueryTrade/V2").Should().Be(1);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Fact]
    public async Task Readiness_ConfigFailClosed_MissingConfig_ThrowsBeforeHttp()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        var (provider, httpClient) = server.CreateProvider(MerchantId, creditCheckCode: "");
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300016",
                ProviderAuthorizationReference: "gwsr30000016",
                Amount: 300m,
                Currency: "TWD");

            var act = () => provider.CheckExecutionReadinessAsync(request);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*CreditCheckCode*");
            server.RecordedRequests.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Readiness_ConfigFailClosed_MissingDoActionUrlWhenActionable_DoesNotReturnReady()
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse("""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132300017",
                "status": "已授權",
                "close_data": []
            }
        }
        """);

        var options = new ECPayRefundOptions
        {
            MerchantId = MerchantId,
            HashKey = HashKey,
            HashIv = HashIv,
            CreditCheckCode = CreditCheckCode,
            QueryTradeUrl = $"{server.BaseUrl}/QueryTrade/V2",
            DoActionUrl = null // 缺少 DoActionUrl
        };

        var httpClient = new HttpClient();
        using (httpClient)
        {
            var provider = new ECPayRefundProvider(httpClient, options);
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132300017",
                ProviderAuthorizationReference: "gwsr30000017",
                Amount: 300m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            // 狀態為可操作，但 DoActionUrl 缺失，絕不能回傳 Ready
            result.Outcome.Should().NotBe(RefundReadinessOutcome.Ready);
            result.Outcome.Should().Be(RefundReadinessOutcome.ManualProviderResolutionRequired);
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    [Theory]
    [InlineData(null, "2000132300018", "TWD", 100)]
    [InlineData("", "2000132300018", "TWD", 100)]
    [InlineData("gwsr123", null, "TWD", 100)]
    [InlineData("gwsr123", "", "TWD", 100)]
    [InlineData("gwsr123", "2000132300018", "USD", 100)]
    [InlineData("gwsr123", "2000132300018", "TWD", 0)]
    [InlineData("gwsr123", "2000132300018", "TWD", -50)]
    [InlineData("gwsr123", "2000132300018", "TWD", 99.9)]
    public async Task Readiness_RequestFailClosed_InvalidInputs_ReturnsManualWithoutHttp(
        string? gwsr, string? tradeNo, string currency, decimal amount)
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: tradeNo,
                ProviderAuthorizationReference: gwsr,
                Amount: amount,
                Currency: currency);

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(RefundReadinessOutcome.ManualProviderResolutionRequired);
            server.RecordedRequests.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData("已授權", RefundReadinessOutcome.Ready)]
    [InlineData("已關帳", RefundReadinessOutcome.Ready)]
    [InlineData("已取消", RefundReadinessOutcome.AlreadyRefunded)]
    [InlineData("未授權", RefundReadinessOutcome.ManualProviderResolutionRequired)]
    [InlineData("操作取消", RefundReadinessOutcome.ManualProviderResolutionRequired)]
    [InlineData("未知狀態", RefundReadinessOutcome.Unresolved)]
    public async Task Readiness_UniversalInvariant_DoActionCountAlwaysZero(
        string status, RefundReadinessOutcome expectedOutcome)
    {
        using var server = new FakeECPayServer(HashKey, HashIv);
        server.SetQueryTradeResponse($$"""
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132399999",
                "status": "{{status}}",
                "close_data": []
            }
        }
        """);

        var (provider, httpClient) = server.CreateProvider(MerchantId, CreditCheckCode);
        using (httpClient)
        {
            var request = new RefundExecutionRequest(
                new PaymentAttemptId(Guid.NewGuid()),
                ProviderTransactionId: "2000132399999",
                ProviderAuthorizationReference: "gwsr39999999",
                Amount: 500m,
                Currency: "TWD");

            var result = await provider.CheckExecutionReadinessAsync(request);

            result.Outcome.Should().Be(expectedOutcome);
            // Universal Invariant: 在所有情境下 DoAction 次數恆為 0！
            server.CountCallsByPath("/DoAction").Should().Be(0);
        }
    }

    #endregion
}

public sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

/// <summary>
/// 輕量且確定性之本機 Loopback HTTP 伺服器，模擬綠界官方退款合約。
/// 捕捉真實 HTTP 呼叫之 Method, Path, Headers, Form fields, CheckMacValue, 呼叫次數與呼叫順序。
/// </summary>
public sealed class FakeECPayServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;
    private readonly string _hashKey;
    private readonly string _hashIv;

    public string BaseUrl { get; }
    private readonly ConcurrentQueue<RecordedHttpRequest> _recordedRequests = new();
    public IReadOnlyList<RecordedHttpRequest> RecordedRequests => _recordedRequests.ToList();

    private string _queryTradeResponse = "{}";
    private readonly ConcurrentDictionary<string, string> _doActionResponses = new();
    private readonly ConcurrentDictionary<string, (HttpStatusCode StatusCode, string Content)> _customResponses = new();

    public FakeECPayServer(string hashKey, string hashIv)
    {
        _hashKey = hashKey;
        _hashIv = hashIv;

        var port = FindFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        _listener = new HttpListener();
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();

        _listenTask = Task.Run(ListenLoopAsync);
    }

    public IReadOnlyList<RecordedHttpRequest> GetRecordedRequests() =>
        _recordedRequests.ToList();

    public void SetQueryTradeResponse(string json) => _queryTradeResponse = json;

    public void SetDoActionResponse(string action, string responseBody) =>
        _doActionResponses[action] = responseBody;

    public void SetCustomResponse(string path, HttpStatusCode statusCode, string content) =>
        _customResponses[path] = (statusCode, content);

    public int CountCallsByPath(string path) =>
        _recordedRequests.Count(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));

    public int CountDoActionCalls(string action) =>
        _recordedRequests.Count(r =>
            string.Equals(r.Path, "/DoAction", StringComparison.OrdinalIgnoreCase) &&
            r.Form.TryGetValue("Action", out var a) &&
            string.Equals(a, action, StringComparison.OrdinalIgnoreCase));

    public (ECPayRefundProvider Provider, HttpClient HttpClient) CreateProvider(
        string merchantId,
        string creditCheckCode,
        TimeProvider? timeProvider = null)
    {
        var options = new ECPayRefundOptions
        {
            MerchantId = merchantId,
            HashKey = _hashKey,
            HashIv = _hashIv,
            CreditCheckCode = creditCheckCode,
            QueryTradeUrl = $"{BaseUrl}/QueryTrade/V2",
            DoActionUrl = $"{BaseUrl}/DoAction"
        };

        var httpClient = new HttpClient();
        var provider = new ECPayRefundProvider(httpClient, options, timeProvider);
        return (provider, httpClient);
    }

    private async Task ListenLoopAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested && _listener.IsListening)
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context));
            }
        }
        catch (HttpListenerException)
        {
            // Listener stopped
        }
        catch (ObjectDisposedException)
        {
            // Disposed
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url?.AbsolutePath ?? "";

        // 讀取 Raw Form Body
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        var form = ParseFormUrlEncoded(rawBody);
        var isCheckMacValid = ECPayCheckMacValue.Verify(form, _hashKey, _hashIv);

        _recordedRequests.Enqueue(new RecordedHttpRequest(
            Method: request.HttpMethod,
            Path: path,
            ContentType: request.ContentType ?? "",
            RawBody: rawBody,
            Form: form,
            IsCheckMacValid: isCheckMacValid));

        var response = context.Response;

        if (_customResponses.TryGetValue(path, out var custom))
        {
            response.StatusCode = (int)custom.StatusCode;
            var bytes = Encoding.UTF8.GetBytes(custom.Content);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
            return;
        }

        if (string.Equals(path, "/QueryTrade/V2", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 200;
            response.ContentType = "application/json; charset=utf-8";
            var bytes = Encoding.UTF8.GetBytes(_queryTradeResponse);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
            return;
        }

        if (string.Equals(path, "/DoAction", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";

            form.TryGetValue("Action", out var action);
            var responseBody = action != null && _doActionResponses.TryGetValue(action, out var customResp)
                ? customResp
                : "RtnCode=1&RtnMsg=DefaultSuccess";

            var bytes = Encoding.UTF8.GetBytes(responseBody);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
            return;
        }

        response.StatusCode = 404;
        response.Close();
    }

    private static Dictionary<string, string> ParseFormUrlEncoded(string raw)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var parsed = HttpUtility.ParseQueryString(raw);
        foreach (var key in parsed.AllKeys)
        {
            if (key != null)
            {
                dict[key] = parsed[key] ?? "";
            }
        }
        return dict;
    }

    private static int FindFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }
            _listener.Close();
        }
        catch
        {
            // Suppress dispose errors
        }
    }
}

public sealed record RecordedHttpRequest(
    string Method,
    string Path,
    string ContentType,
    string RawBody,
    IReadOnlyDictionary<string, string> Form,
    bool IsCheckMacValid);
