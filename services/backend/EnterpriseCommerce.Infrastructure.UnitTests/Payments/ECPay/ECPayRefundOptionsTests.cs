using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayRefundOptionsTests
{
    [Fact]
    public void Validate_WhenAllFieldsValidWithLoopbackUrl_DoesNotThrow()
    {
        var options = new ECPayRefundOptions
        {
            MerchantId = "3002607",
            HashKey = "pwFHCqoQZGm3CC6v",
            HashIv = "EkRm7iFT261dpevs",
            CreditCheckCode = "123456",
            QueryTradeUrl = "http://127.0.0.1:5000/QueryTrade/V2",
            DoActionUrl = "http://127.0.0.1:5000/DoAction"
        };

        var act = () => options.Validate(requireDoActionUrl: true);

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_WhenAllFieldsValidWithHttpsUrl_DoesNotThrow()
    {
        var options = new ECPayRefundOptions
        {
            MerchantId = "3002607",
            HashKey = "pwFHCqoQZGm3CC6v",
            HashIv = "EkRm7iFT261dpevs",
            CreditCheckCode = "123456",
            QueryTradeUrl = "https://custom-refund-gateway.internal/QueryTrade/V2",
            DoActionUrl = "https://custom-refund-gateway.internal/DoAction"
        };

        var act = () => options.Validate(requireDoActionUrl: true);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenMerchantIdMissing_ThrowsInvalidOperationExceptionWithoutSecrets(string? merchantId)
    {
        var options = CreateValidOptions();
        options.MerchantId = merchantId;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*MerchantId*")
            .And.Message.Should().NotContain(options.HashKey)
            .And.NotContain(options.HashIv)
            .And.NotContain(options.CreditCheckCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenHashKeyMissing_ThrowsInvalidOperationExceptionWithoutSecrets(string? hashKey)
    {
        var options = CreateValidOptions();
        options.HashKey = hashKey;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HashKey*")
            .And.Message.Should().NotContain(options.CreditCheckCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenHashIvMissing_ThrowsInvalidOperationExceptionWithoutSecrets(string? hashIv)
    {
        var options = CreateValidOptions();
        options.HashIv = hashIv;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HashIv*")
            .And.Message.Should().NotContain(options.CreditCheckCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenCreditCheckCodeMissing_ThrowsInvalidOperationException(string? creditCheckCode)
    {
        var options = CreateValidOptions();
        options.CreditCheckCode = creditCheckCode;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*CreditCheckCode*")
            .And.Message.Should().NotContain(options.HashKey)
            .And.NotContain(options.HashIv);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-valid-url")]
    [InlineData("relative/path/url")]
    public void Validate_WhenQueryTradeUrlInvalid_ThrowsInvalidOperationException(string? url)
    {
        var options = CreateValidOptions();
        options.QueryTradeUrl = url;

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*QueryTradeUrl*");
    }

    [Fact]
    public void Validate_WhenQueryTradeUrlHttpNonLoopback_ThrowsInvalidOperationException()
    {
        var options = CreateValidOptions();
        options.QueryTradeUrl = "http://insecure-remote.com/QueryTrade/V2";

        var act = () => options.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HTTPS*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid-url")]
    public void Validate_WhenDoActionUrlRequiredAndInvalid_ThrowsInvalidOperationException(string? url)
    {
        var options = CreateValidOptions();
        options.DoActionUrl = url;

        var act = () => options.Validate(requireDoActionUrl: true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*DoActionUrl*");
    }

    [Fact]
    public void Validate_WhenDoActionUrlHttpNonLoopback_ThrowsInvalidOperationException()
    {
        var options = CreateValidOptions();
        options.DoActionUrl = "http://insecure-remote.com/DoAction";

        var act = () => options.Validate(requireDoActionUrl: true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HTTPS*");
    }

    [Fact]
    public void Validate_WhenDoActionUrlNotRequired_DoesNotThrowEvenIfDoActionUrlEmpty()
    {
        var options = CreateValidOptions();
        options.DoActionUrl = null;

        var act = () => options.Validate(requireDoActionUrl: false);

        act.Should().NotThrow();
    }

    private static ECPayRefundOptions CreateValidOptions() => new()
    {
        MerchantId = "3002607",
        HashKey = "pwFHCqoQZGm3CC6v",
        HashIv = "EkRm7iFT261dpevs",
        CreditCheckCode = "123456",
        QueryTradeUrl = "http://127.0.0.1:5000/QueryTrade/V2",
        DoActionUrl = "http://127.0.0.1:5000/DoAction"
    };
}
