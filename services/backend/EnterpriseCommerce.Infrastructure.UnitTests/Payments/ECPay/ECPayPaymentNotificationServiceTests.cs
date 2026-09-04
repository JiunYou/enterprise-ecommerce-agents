using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayPaymentNotificationServiceTests
{
    private const string MerchantId = "synthetic_merchant_123";
    private const string HashKey = "synthetic_hashkey_456";
    private const string HashIv = "synthetic_hashiv_789";

    private static ECPayPaymentNotificationService CreateService()
    {
        var options = Options.Create(new ECPayPaymentOptions
        {
            MerchantId = MerchantId,
            HashKey = HashKey,
            HashIv = HashIv,
            ReturnUrl = "https://shop.example.com/api/v1/payments/webhooks/ecpay"
        });

        var configurationMock = new Mock<IConfiguration>();
        return new ECPayPaymentNotificationService(options, configurationMock.Object);
    }

    private static Dictionary<string, string> CreateValidPayload(
        Guid attemptGuid,
        string tradeNo = "ECPAY_TEST_TXN_001",
        string tradeAmt = "500",
        string rtnCode = "1",
        string simulatePaid = "0")
    {
        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = MerchantId,
            ["MerchantTradeNo"] = "MTRADENO1234567890",
            ["RtnCode"] = rtnCode,
            ["RtnMsg"] = "Succeeded",
            ["TradeNo"] = tradeNo,
            ["TradeAmt"] = tradeAmt,
            ["PaymentDate"] = "2026/09/04 16:00:00",
            ["PaymentType"] = "Credit_CreditCard",
            ["TradeDate"] = "2026/09/04 15:59:00",
            ["SimulatePaid"] = simulatePaid,
            ["CustomField1"] = attemptGuid.ToString(),
            ["CustomField2"] = Guid.NewGuid().ToString()
        };

        var checkMac = ECPayCheckMacValue.Generate(fields, HashKey, HashIv);
        fields["CheckMacValue"] = checkMac;
        return fields;
    }

    [Fact]
    public void VerifyAndParseNotification_WhenValidSuccessNotification_ReturnsExpectedCommand()
    {
        var service = CreateService();
        var attemptGuid = Guid.NewGuid();
        var payload = CreateValidPayload(attemptGuid, tradeNo: "TXN_777888", tradeAmt: "1500", rtnCode: "1", simulatePaid: "0");

        var command = service.VerifyAndParseNotification(payload);

        command.Should().NotBeNull();
        command!.PaymentAttemptId.Should().Be(attemptGuid);
        command.Provider.Should().Be("ECPay");
        command.ProviderEventId.Should().Be("TXN_777888");
        command.ProviderTransactionId.Should().Be("TXN_777888");
        command.Amount.Should().Be(1500m);
        command.Currency.Should().Be("TWD");
        command.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void VerifyAndParseNotification_WhenCheckMacValueMissing_ThrowsException()
    {
        var service = CreateService();
        var payload = CreateValidPayload(Guid.NewGuid());
        payload.Remove("CheckMacValue");

        var act = () => service.VerifyAndParseNotification(payload);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*Missing required CheckMacValue*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenCheckMacValueInvalid_ThrowsException()
    {
        var service = CreateService();
        var payload = CreateValidPayload(Guid.NewGuid());
        payload["CheckMacValue"] = "INVALID_HASH_VALUE_1234567890ABCDEF";

        var act = () => service.VerifyAndParseNotification(payload);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*Invalid CheckMacValue signature*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenAmountTampered_ThrowsException()
    {
        var service = CreateService();
        var payload = CreateValidPayload(Guid.NewGuid(), tradeAmt: "500");
        // Tamper amount without recalculating CheckMacValue
        payload["TradeAmt"] = "100";

        var act = () => service.VerifyAndParseNotification(payload);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*Invalid CheckMacValue signature*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenAttemptIdTampered_ThrowsException()
    {
        var service = CreateService();
        var payload = CreateValidPayload(Guid.NewGuid());
        // Tamper CustomField1 without recalculating CheckMacValue
        payload["CustomField1"] = Guid.NewGuid().ToString();

        var act = () => service.VerifyAndParseNotification(payload);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*Invalid CheckMacValue signature*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenRtnCodeTampered_ThrowsException()
    {
        var service = CreateService();
        var payload = CreateValidPayload(Guid.NewGuid(), rtnCode: "0");
        // Tamper RtnCode to 1 without recalculating CheckMacValue
        payload["RtnCode"] = "1";

        var act = () => service.VerifyAndParseNotification(payload);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*Invalid CheckMacValue signature*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenTradeNoTampered_ThrowsException()
    {
        var service = CreateService();
        var payload = CreateValidPayload(Guid.NewGuid(), tradeNo: "TXN_ORIG");
        payload["TradeNo"] = "TXN_FORGED";

        var act = () => service.VerifyAndParseNotification(payload);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*Invalid CheckMacValue signature*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenMerchantIdMismatch_ThrowsException()
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = "foreign_merchant_999",
            ["RtnCode"] = "1",
            ["TradeNo"] = "TXN_123",
            ["TradeAmt"] = "500",
            ["CustomField1"] = Guid.NewGuid().ToString()
        };
        // Sign with our HashKey/HashIv, but with a different MerchantID
        fields["CheckMacValue"] = ECPayCheckMacValue.Generate(fields, HashKey, HashIv);

        var act = () => service.VerifyAndParseNotification(fields);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*MerchantID mismatch*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenSimulatePaidIsOne_ReturnsNull()
    {
        var service = CreateService();
        var attemptGuid = Guid.NewGuid();
        // Valid signature, RtnCode=1, but SimulatePaid=1
        var payload = CreateValidPayload(attemptGuid, rtnCode: "1", simulatePaid: "1");

        var command = service.VerifyAndParseNotification(payload);

        // Crucial security requirement: SimulatePaid == 1 must NEVER dispatch a payment success command
        command.Should().BeNull();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("2")]
    public void VerifyAndParseNotification_WhenRtnCodeNotOne_ReturnsNull(string nonSuccessRtnCode)
    {
        var service = CreateService();
        var attemptGuid = Guid.NewGuid();
        // Valid signature, but RtnCode != 1
        var payload = CreateValidPayload(attemptGuid, rtnCode: nonSuccessRtnCode, simulatePaid: "0");

        var command = service.VerifyAndParseNotification(payload);

        // Conservative policy in P3: non-1 RtnCode does not dispatch success command
        command.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("   ")]
    public void VerifyAndParseNotification_WhenCustomField1InvalidGuid_ThrowsException(string malformedCustomField1)
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = MerchantId,
            ["RtnCode"] = "1",
            ["TradeNo"] = "TXN_123",
            ["TradeAmt"] = "500",
            ["CustomField1"] = malformedCustomField1
        };
        fields["CheckMacValue"] = ECPayCheckMacValue.Generate(fields, HashKey, HashIv);

        var act = () => service.VerifyAndParseNotification(fields);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*CustomField1*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void VerifyAndParseNotification_WhenTradeNoMissing_ThrowsException(string emptyTradeNo)
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = MerchantId,
            ["RtnCode"] = "1",
            ["TradeNo"] = emptyTradeNo,
            ["TradeAmt"] = "500",
            ["CustomField1"] = Guid.NewGuid().ToString()
        };
        fields["CheckMacValue"] = ECPayCheckMacValue.Generate(fields, HashKey, HashIv);

        var act = () => service.VerifyAndParseNotification(fields);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*TradeNo*");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-100")]
    [InlineData("100.50")]
    [InlineData("invalid-amount")]
    public void VerifyAndParseNotification_WhenTradeAmtInvalid_ThrowsException(string invalidTradeAmt)
    {
        var service = CreateService();
        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = MerchantId,
            ["RtnCode"] = "1",
            ["TradeNo"] = "TXN_123",
            ["TradeAmt"] = invalidTradeAmt,
            ["CustomField1"] = Guid.NewGuid().ToString()
        };
        fields["CheckMacValue"] = ECPayCheckMacValue.Generate(fields, HashKey, HashIv);

        var act = () => service.VerifyAndParseNotification(fields);

        act.Should().Throw<ECPayNotificationValidationException>()
            .WithMessage("*TradeAmt*");
    }

    [Fact]
    public void VerifyAndParseNotification_WhenCustomField1UsesNFormat_ParsesToExactOriginalPaymentAttemptId()
    {
        var service = CreateService();
        var originalAttemptGuid = Guid.NewGuid();
        var nFormatAttemptId = originalAttemptGuid.ToString("N");
        var nFormatOrderId = Guid.NewGuid().ToString("N");

        var fields = new Dictionary<string, string>
        {
            ["MerchantID"] = MerchantId,
            ["MerchantTradeNo"] = "MTRADENO1234567890",
            ["RtnCode"] = "1",
            ["RtnMsg"] = "Succeeded",
            ["TradeNo"] = "TXN_999888",
            ["TradeAmt"] = "100",
            ["CustomField1"] = nFormatAttemptId,
            ["CustomField2"] = nFormatOrderId
        };
        fields["CheckMacValue"] = ECPayCheckMacValue.Generate(fields, HashKey, HashIv);

        var command = service.VerifyAndParseNotification(fields);

        command.Should().NotBeNull();
        command!.PaymentAttemptId.Should().Be(originalAttemptGuid);
    }
}
