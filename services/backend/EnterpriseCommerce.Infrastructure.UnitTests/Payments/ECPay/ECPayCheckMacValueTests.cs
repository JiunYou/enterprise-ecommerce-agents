using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;
using Xunit;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayCheckMacValueTests
{
    [Fact]
    public void Generate_MatchesOfficialPublishedVector()
    {
        // Vector published in official ECPay AIO CheckMacValue documentation (post 16623)
        var parameters = new Dictionary<string, string>
        {
            ["ChoosePayment"] = "ALL",
            ["EncryptType"] = "1",
            ["ItemName"] = "Apple iphone 15",
            ["MerchantID"] = "3002607",
            ["MerchantTradeDate"] = "2023/03/12 15:30:23",
            ["MerchantTradeNo"] = "ecpay20230312153023",
            ["PaymentType"] = "aio",
            ["ReturnURL"] = "https://www.ecpay.com.tw/receive.php",
            ["TotalAmount"] = "30000",
            ["TradeDesc"] = "促銷方案"
        };

        var hashKey = "pwFHCqoQZGmho4w6";
        var hashIv = "EkRm7iFT261dpevs";

        var result = ECPayCheckMacValue.Generate(parameters, hashKey, hashIv);

        result.Should().Be("6C51C9E6888DE861FD62FB1DD17029FC742634498FD813DC43D4243B5685B840");
    }

    [Fact]
    public void Generate_ExcludesCheckMacValueFromItsOwnCalculation()
    {
        var parametersWithoutCheckMac = new Dictionary<string, string>
        {
            ["MerchantID"] = "test_merchant",
            ["TotalAmount"] = "100",
            ["TradeDesc"] = "Test Order"
        };

        var parametersWithCheckMac = new Dictionary<string, string>(parametersWithoutCheckMac)
        {
            ["CheckMacValue"] = "SOME_PREVIOUS_OR_DUMMY_VALUE"
        };

        var hashKey = "synthetic_key_123456";
        var hashIv = "synthetic_iv_1234567";

        var result1 = ECPayCheckMacValue.Generate(parametersWithoutCheckMac, hashKey, hashIv);
        var result2 = ECPayCheckMacValue.Generate(parametersWithCheckMac, hashKey, hashIv);

        result1.Should().Be(result2);
    }

    [Fact]
    public void Generate_WhenSignedParameterChanges_ProducesDifferentCheckMacValue()
    {
        var parameters1 = new Dictionary<string, string>
        {
            ["MerchantID"] = "test_merchant",
            ["TotalAmount"] = "100"
        };

        var parameters2 = new Dictionary<string, string>
        {
            ["MerchantID"] = "test_merchant",
            ["TotalAmount"] = "200"
        };

        var hashKey = "synthetic_key_123456";
        var hashIv = "synthetic_iv_1234567";

        var result1 = ECPayCheckMacValue.Generate(parameters1, hashKey, hashIv);
        var result2 = ECPayCheckMacValue.Generate(parameters2, hashKey, hashIv);

        result1.Should().NotBe(result2);
    }

    [Theory]
    [InlineData(null, "iv")]
    [InlineData("", "iv")]
    [InlineData("   ", "iv")]
    [InlineData("key", null)]
    [InlineData("key", "")]
    [InlineData("key", "   ")]
    public void Generate_WhenKeyOrIvMissing_ThrowsArgumentException(string? key, string? iv)
    {
        var parameters = new Dictionary<string, string>
        {
            ["MerchantID"] = "test_merchant"
        };

        var act = () => ECPayCheckMacValue.Generate(parameters, key!, iv!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Generate_FullProductionShapedPayload_MatchesIndependentOracleDigest()
    {
        // Full production-shaped payload with:
        // - HTTPS ReturnURL
        // - ClientBackURL with query string (?payment=returned)
        // - CustomField1 and CustomField2 using 32-char N-format Guids
        // - Spaces in TradeDesc and ItemName
        // - ChoosePayment=Credit, EncryptType=1
        var payload = new Dictionary<string, string>
        {
            ["MerchantID"] = "3002607",
            ["MerchantTradeNo"] = "4500685CB319B6C658EA",
            ["MerchantTradeDate"] = "2026/09/04 18:06:52",
            ["PaymentType"] = "aio",
            ["TotalAmount"] = "100",
            ["TradeDesc"] = "EnterpriseCommerce Order",
            ["ItemName"] = "EnterpriseCommerce Order",
            ["ReturnURL"] = "https://asked-recreational-broken-assessed.trycloudflare.com/api/v1/payments/webhooks/ecpay",
            ["ChoosePayment"] = "Credit",
            ["ClientBackURL"] = "http://localhost:3001/orders/dc938f5b-6371-4c01-aada-e59b11deca59?payment=returned",
            ["EncryptType"] = "1",
            ["CustomField1"] = "5ab0244884df4e54af9c24d0ce077cf",
            ["CustomField2"] = "dc938f5b63714c01aadae59b11deca59"
        };

        var stageKey = "pwFHCqoQZGmho4w6";
        var stageIv = "EkRm7iFT261dpevs";

        // Calculated independently by /tmp/enterprisecommerce-ecpay-checkmac-r1-oracle/oracle.py
        const string expectedOracleMac = "3F145F6AD70096364CBC825C4E91FAD79031C6F713D24A48D743F855848132C3";

        var csharpMac = ECPayCheckMacValue.Generate(payload, stageKey, stageIv);

        csharpMac.Should().Be(expectedOracleMac);
    }

    [Fact]
    public void Sorting_OrdinalVsIgnoreCase_ProducesIdenticalKeyOrder_ForActualAioFieldSet()
    {
        var actualAioKeys = new[]
        {
            "MerchantID",
            "MerchantTradeNo",
            "MerchantTradeDate",
            "PaymentType",
            "TotalAmount",
            "TradeDesc",
            "ItemName",
            "ReturnURL",
            "ChoosePayment",
            "ClientBackURL",
            "EncryptType",
            "CustomField1",
            "CustomField2"
        };

        var ignoreCaseOrder = actualAioKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        var ordinalOrder = actualAioKeys.OrderBy(k => k, StringComparer.Ordinal).ToArray();

        ignoreCaseOrder.Should().Equal(ordinalOrder, "Ordinal and OrdinalIgnoreCase produce identical order for the actual AIO field set");
    }
}
