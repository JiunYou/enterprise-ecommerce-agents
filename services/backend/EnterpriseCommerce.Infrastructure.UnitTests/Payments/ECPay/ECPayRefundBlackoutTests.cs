using EnterpriseCommerce.Infrastructure.Payments.ECPay;
using FluentAssertions;

namespace EnterpriseCommerce.Infrastructure.UnitTests.Payments.ECPay;

public class ECPayRefundBlackoutTests
{
    [Theory]
    // 2026-09-07 20:14:59 Taiwan time -> UTC 12:14:59
    [InlineData(2026, 9, 7, 12, 14, 59, false)]
    // 2026-09-07 20:15:00 Taiwan time -> UTC 12:15:00 (blocked)
    [InlineData(2026, 9, 7, 12, 15, 0, true)]
    // 2026-09-07 20:22:30 Taiwan time -> UTC 12:22:30 (blocked)
    [InlineData(2026, 9, 7, 12, 22, 30, true)]
    // 2026-09-07 20:29:59 Taiwan time -> UTC 12:29:59 (blocked)
    [InlineData(2026, 9, 7, 12, 29, 59, true)]
    // 2026-09-07 20:30:00 Taiwan time -> UTC 12:30:00 (allowed)
    [InlineData(2026, 9, 7, 12, 30, 0, false)]
    // 2026-09-07 10:00:00 Taiwan time -> UTC 02:00:00 (allowed)
    [InlineData(2026, 9, 7, 2, 0, 0, false)]
    public void IsInTaiwanBlackoutWindow_ExactBoundaries_EvaluatesCorrectly(
        int year, int month, int day, int utcHour, int utcMinute, int utcSecond, bool expectedBlocked)
    {
        var utcTime = new DateTimeOffset(year, month, day, utcHour, utcMinute, utcSecond, TimeSpan.Zero);

        var isBlocked = ECPayRefundProvider.IsInTaiwanBlackoutWindow(utcTime);

        isBlocked.Should().Be(expectedBlocked);
    }
}
