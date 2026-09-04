using System.Text.RegularExpressions;
using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Domain.Orders.ValueObjects;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayPaymentProviderTests
{
    private static ECPayPaymentOptions CreateValidOptions() => new()
    {
        MerchantId = "synthetic_merchant_123",
        HashKey = "synthetic_hashkey_456",
        HashIv = "synthetic_hashiv_789",
        ReturnUrl = "https://shop.example.com/api/v1/payments/webhooks/ecpay",
        ClientBackUrlBase = "https://shop.example.com",
        ActionUrl = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5"
    };

    [Fact]
    public async Task InitiatePaymentAsync_ReturnsExpectedPostLaunchContract()
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 9, 4, 7, 30, 15, TimeSpan.Zero); // 15:30:15 in Taiwan (UTC+8)

        var response = await provider.InitiatePaymentAsync(attemptId, orderId, 1500m, "TWD", createdAt);

        response.Should().NotBeNull();
        response.ProviderTransactionId.Should().BeNull();
        response.Method.Should().Be(PaymentLaunchMethod.Post);
        response.ActionUrl.Should().Be("https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5");
        response.FormFields.Should().NotBeNull();

        var fields = response.FormFields!;
        fields["MerchantID"].Should().Be("synthetic_merchant_123");
        fields["MerchantTradeNo"].Should().HaveLength(20);
        Regex.IsMatch(fields["MerchantTradeNo"], "^[A-Za-z0-9]+$").Should().BeTrue();
        fields["MerchantTradeDate"].Should().Be("2026/09/04 15:30:15");
        fields["PaymentType"].Should().Be("aio");
        fields["TotalAmount"].Should().Be("1500");
        fields["TradeDesc"].Should().Be("EnterpriseCommerce Order");
        fields["ItemName"].Should().Be("EnterpriseCommerce Order");
        fields["ReturnURL"].Should().Be("https://shop.example.com/api/v1/payments/webhooks/ecpay");
        fields["ChoosePayment"].Should().Be("Credit");
        fields["ClientBackURL"].Should().Be($"https://shop.example.com/orders/{orderId.Value}?payment=returned");
        fields.Should().NotContainKey("OrderResultURL");
        fields["EncryptType"].Should().Be("1");
        fields["CustomField1"].Should().Be(attemptId.Value.ToString("N"));
        fields["CustomField2"].Should().Be(orderId.Value.ToString("N"));
        fields.Should().ContainKey("CheckMacValue");
        fields["CheckMacValue"].Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(100, "100")]
    [InlineData(500, "500")]
    [InlineData(1, "1")]
    public async Task InitiatePaymentAsync_AcceptsPositiveWholeIntegerTwd(decimal amount, string expectedAmountStr)
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var createdAt = DateTimeOffset.UtcNow;

        var response = await provider.InitiatePaymentAsync(attemptId, orderId, amount, "TWD", createdAt);

        response.FormFields!["TotalAmount"].Should().Be(expectedAmountStr);
    }

    [Theory]
    [InlineData(100.50)]
    [InlineData(10.01)]
    [InlineData(0.99)]
    public async Task InitiatePaymentAsync_WhenAmountHasFractionalUnits_ThrowsArgumentOutOfRangeException(decimal fractionalAmount)
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());

        var act = () => provider.InitiatePaymentAsync(attemptId, orderId, fractionalAmount, "TWD", DateTimeOffset.UtcNow);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
            .WithMessage("*whole integer*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task InitiatePaymentAsync_WhenAmountIsZeroOrNegative_ThrowsArgumentOutOfRangeException(decimal nonPositiveAmount)
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());

        var act = () => provider.InitiatePaymentAsync(attemptId, orderId, nonPositiveAmount, "TWD", DateTimeOffset.UtcNow);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("JPY")]
    [InlineData("EUR")]
    [InlineData("")]
    public async Task InitiatePaymentAsync_WhenCurrencyIsNotTwd_ThrowsNotSupportedException(string unsupportedCurrency)
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());

        var act = () => provider.InitiatePaymentAsync(attemptId, orderId, 100m, unsupportedCurrency, DateTimeOffset.UtcNow);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*only supports TWD*");
    }

    [Fact]
    public async Task InitiatePaymentAsync_ForSamePaymentAttempt_ProducesDeterministicRetryPayload()
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

        // First initiation
        var response1 = await provider.InitiatePaymentAsync(attemptId, orderId, 500m, "TWD", createdAt);

        // Retry initiation (reusing the same PaymentAttempt)
        var response2 = await provider.InitiatePaymentAsync(attemptId, orderId, 500m, "TWD", createdAt);

        response1.FormFields!["MerchantTradeNo"].Should().Be(response2.FormFields!["MerchantTradeNo"]);
        response1.FormFields!["MerchantTradeDate"].Should().Be(response2.FormFields!["MerchantTradeDate"]);
        response1.FormFields!["TotalAmount"].Should().Be(response2.FormFields!["TotalAmount"]);
        response1.FormFields!["CustomField1"].Should().Be(response2.FormFields!["CustomField1"]);
        response1.FormFields!["CustomField2"].Should().Be(response2.FormFields!["CustomField2"]);
        response1.FormFields!["CheckMacValue"].Should().Be(response2.FormFields!["CheckMacValue"]);
    }

    [Fact]
    public async Task InitiatePaymentAsync_ChangingClientBackUrl_ChangesCheckMacValue()
    {
        var options1 = CreateValidOptions();
        options1.ClientBackUrlBase = "https://shop.example.com";
        var provider1 = new ECPayPaymentProvider(options1);

        var options2 = CreateValidOptions();
        options2.ClientBackUrlBase = "https://other.example.com";
        var provider2 = new ECPayPaymentProvider(options2);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var createdAt = new DateTimeOffset(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

        var response1 = await provider1.InitiatePaymentAsync(attemptId, orderId, 500m, "TWD", createdAt);
        var response2 = await provider2.InitiatePaymentAsync(attemptId, orderId, 500m, "TWD", createdAt);

        response1.FormFields!["ClientBackURL"].Should().NotBe(response2.FormFields!["ClientBackURL"]);
        response1.FormFields!["CheckMacValue"].Should().NotBe(response2.FormFields!["CheckMacValue"],
            "Because ClientBackURL is an authoritative signed parameter and must be included before CheckMacValue calculation");
    }

    [Theory]
    [InlineData(null, "key", "iv", "https://return.url", "https://client.url", "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5")]
    [InlineData("mid", null, "iv", "https://return.url", "https://client.url", "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5")]
    [InlineData("mid", "key", null, "https://return.url", "https://client.url", "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5")]
    [InlineData("mid", "key", "iv", null, "https://client.url", "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5")]
    [InlineData("mid", "key", "iv", "https://return.url", null, "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5")]
    [InlineData("mid", "key", "iv", "https://return.url", "not-a-valid-url", "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5")]
    [InlineData("mid", "key", "iv", "https://return.url", "https://client.url", "http://insecure-http.url")]
    public async Task InitiatePaymentAsync_WhenOptionsInvalidOrIncomplete_ThrowsInvalidOperationException(
        string? mid, string? key, string? iv, string? returnUrl, string? clientBackUrl, string? actionUrl)
    {
        var options = new ECPayPaymentOptions
        {
            MerchantId = mid,
            HashKey = key,
            HashIv = iv,
            ReturnUrl = returnUrl,
            ClientBackUrlBase = clientBackUrl,
            ActionUrl = actionUrl!
        };
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());

        var act = () => provider.InitiatePaymentAsync(attemptId, orderId, 100m, "TWD", DateTimeOffset.UtcNow);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InitiatePaymentAsync_CustomFields_MustNotContainHyphenAndMustBeAlphanumeric()
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var createdAt = DateTimeOffset.UtcNow;

        var response = await provider.InitiatePaymentAsync(attemptId, orderId, 100m, "TWD", createdAt);

        var fields = response.FormFields!;
        fields["CustomField1"].Should().NotContain("-", "ECPay AIO specification prohibits hyphens in CustomField");
        fields["CustomField2"].Should().NotContain("-", "ECPay AIO specification prohibits hyphens in CustomField");
        Regex.IsMatch(fields["CustomField1"], "^[A-Za-z0-9]+$").Should().BeTrue();
        Regex.IsMatch(fields["CustomField2"], "^[A-Za-z0-9]+$").Should().BeTrue();
    }

    [Fact]
    public async Task InitiatePaymentAsync_GeneratedFields_ComplyWithOfficialAioConstraints()
    {
        var options = CreateValidOptions();
        var provider = new ECPayPaymentProvider(options);

        var attemptId = new PaymentAttemptId(Guid.NewGuid());
        var orderId = new OrderId(Guid.NewGuid());
        var createdAt = DateTimeOffset.UtcNow;

        var response = await provider.InitiatePaymentAsync(attemptId, orderId, 250m, "TWD", createdAt);
        var fields = response.FormFields!;

        // MerchantTradeNo: <= 20 alphanumeric
        fields["MerchantTradeNo"].Length.Should().BeLessOrEqualTo(20);
        Regex.IsMatch(fields["MerchantTradeNo"], "^[A-Za-z0-9]+$").Should().BeTrue();

        // CustomField1: <= 50 alphanumeric
        fields["CustomField1"].Length.Should().BeLessOrEqualTo(50);
        Regex.IsMatch(fields["CustomField1"], "^[A-Za-z0-9]+$").Should().BeTrue();

        // CustomField2: <= 50 alphanumeric
        fields["CustomField2"].Length.Should().BeLessOrEqualTo(50);
        Regex.IsMatch(fields["CustomField2"], "^[A-Za-z0-9]+$").Should().BeTrue();

        // TradeDesc: <= 200, valid text without unsupported special characters
        fields["TradeDesc"].Length.Should().BeLessOrEqualTo(200);
        fields["TradeDesc"].Should().NotBeNullOrWhiteSpace();

        // ItemName: <= 400, valid text
        fields["ItemName"].Length.Should().BeLessOrEqualTo(400);
        fields["ItemName"].Should().NotBeNullOrWhiteSpace();

        // TotalAmount: positive whole integer string
        Regex.IsMatch(fields["TotalAmount"], "^[1-9][0-9]*$").Should().BeTrue();
        fields["TotalAmount"].Should().Be("250");
    }
}
