using EnterpriseCommerce.Application.Payments;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayRefundParserTests
{
    [Fact]
    public void ParseQueryTradeResponse_WhenAuthorizedWithoutCloseData_ReturnsAuthorized()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "已授權",
                "close_data": []
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.Authorized);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenCloseDataContainsToBeCaptured_ReturnsToBeCaptured()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "已授權",
                "close_data": [
                    { "status": "要關帳", "amount": 100 }
                ]
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.ToBeCaptured);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenStatusCaptured_ReturnsCaptured()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "已關帳",
                "close_data": []
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.Captured);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenCloseDataCaptured_ReturnsCaptured()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "已授權",
                "close_data": [
                    { "status": "已關帳", "amount": 100 }
                ]
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.Captured);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenStatusCancelled_ReturnsAlreadyRefunded()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "已取消",
                "close_data": []
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.AlreadyRefunded);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenStatusUnauthorized_ReturnsManualResolutionRequired()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "未授權",
                "close_data": []
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.ManualResolutionRequired);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenStatusActionCancelled_ReturnsManualResolutionRequired()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "操作取消",
                "close_data": []
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.ManualResolutionRequired);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenUnknownStatus_ReturnsUnresolved()
    {
        var json = """
        {
            "RtnMsg": "",
            "RtnValue": {
                "TradeNo": "2000132123456",
                "status": "全新未知狀態字串",
                "close_data": []
            }
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeTrue();
        result.State.Should().Be(ECPayTradeClassifiedState.Unresolved);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenRtnMsgNonEmpty_FailsWithUnresolved()
    {
        var json = """
        {
            "RtnMsg": "查無此筆交易資料",
            "RtnValue": null
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeFalse();
        result.FailureOutcome.Should().Be(RefundExecutionOutcome.Unresolved);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenRtnValueMissing_FailsWithUnresolved()
    {
        var json = """
        {
            "RtnMsg": ""
        }
        """;

        var result = ECPayRefundProvider.ParseQueryTradeResponse(json);

        result.IsSuccess.Should().BeFalse();
        result.FailureOutcome.Should().Be(RefundExecutionOutcome.Unresolved);
    }

    [Fact]
    public void ParseQueryTradeResponse_WhenMalformedJson_FailsWithUnresolved()
    {
        var malformed = "{ this is not valid json }";

        var result = ECPayRefundProvider.ParseQueryTradeResponse(malformed);

        result.IsSuccess.Should().BeFalse();
        result.FailureOutcome.Should().Be(RefundExecutionOutcome.Unresolved);
    }

    [Fact]
    public void ParseDoActionResponse_WhenRtnCode1_ReturnsSuccess()
    {
        var response = "MerchantID=3002607&MerchantTradeNo=TEST1234567890&TradeNo=2000132123456&RtnCode=1&RtnMsg=Success";

        var result = ECPayRefundProvider.ParseDoActionResponse(response);

        result.Outcome.Should().Be(DoActionWireOutcome.Success);
        result.RtnCode.Should().Be("1");
        result.RtnMsg.Should().Be("Success");
    }

    [Theory]
    [InlineData("10200047", "Credit card transaction not found")]
    [InlineData("10200073", "Repeated action not allowed")]
    [InlineData("0", "Action failed")]
    public void ParseDoActionResponse_WhenRtnCodeNot1_ReturnsDeterministicFailure(string rtnCode, string rtnMsg)
    {
        var response = $"MerchantID=3002607&MerchantTradeNo=TEST1234567890&TradeNo=2000132123456&RtnCode={rtnCode}&RtnMsg={rtnMsg}";

        var result = ECPayRefundProvider.ParseDoActionResponse(response);

        result.Outcome.Should().Be(DoActionWireOutcome.DeterministicFailure);
        result.RtnCode.Should().Be(rtnCode);
        result.RtnMsg.Should().Be(rtnMsg);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MerchantID=3002607&TradeNo=123")] // missing RtnCode
    [InlineData("invalid non-parameter format without equal signs")]
    public void ParseDoActionResponse_WhenMalformedOrEmpty_ReturnsUncertain(string raw)
    {
        var result = ECPayRefundProvider.ParseDoActionResponse(raw);

        result.Outcome.Should().Be(DoActionWireOutcome.Uncertain);
    }
}
