using System.Text.RegularExpressions;
using EnterpriseCommerce.Domain.Payments.ValueObjects;
using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;
using Xunit;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayMerchantTradeNoTests
{
    [Fact]
    public void FromPaymentAttemptId_WhenCalledRepeatedlyForSameAttemptId_ReturnsIdenticalResult()
    {
        var attemptId = new PaymentAttemptId(Guid.NewGuid());

        var result1 = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId);
        var result2 = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId);

        result1.Should().Be(result2);
    }

    [Fact]
    public void FromPaymentAttemptId_LengthIsAtMostTwentyCharacters()
    {
        var attemptId = new PaymentAttemptId(Guid.NewGuid());

        var result = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId);

        result.Length.Should().BeLessThanOrEqualTo(20);
        result.Length.Should().Be(20);
    }

    [Fact]
    public void FromPaymentAttemptId_ContainsOnlyAsciiAlphanumericCharacters()
    {
        var attemptId = new PaymentAttemptId(Guid.NewGuid());

        var result = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId);

        Regex.IsMatch(result, "^[A-Za-z0-9]+$").Should().BeTrue();
    }

    [Fact]
    public void FromPaymentAttemptId_ForDifferentAttemptIds_ProducesDifferentOutputs()
    {
        var attemptId1 = new PaymentAttemptId(Guid.NewGuid());
        var attemptId2 = new PaymentAttemptId(Guid.NewGuid());

        var result1 = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId1);
        var result2 = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId2);

        result1.Should().NotBe(result2);
    }

    [Fact]
    public void FromPaymentAttemptId_LargeDeterministicSample_ContainsNoDuplicates()
    {
        const int sampleSize = 10000;
        var generated = new HashSet<string>(sampleSize);

        for (int i = 0; i < sampleSize; i++)
        {
            var attemptId = new PaymentAttemptId(Guid.NewGuid());
            var tradeNo = ECPayMerchantTradeNo.FromPaymentAttemptId(attemptId);

            tradeNo.Length.Should().BeLessThanOrEqualTo(20);
            Regex.IsMatch(tradeNo, "^[A-Za-z0-9]+$").Should().BeTrue();

            generated.Add(tradeNo).Should().BeTrue($"Collision detected on iteration {i} for tradeNo {tradeNo}");
        }
    }
}
