using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EnterpriseCommerce.Infrastructure.Payments.ECPay;

public static class ECPayCheckMacValue
{
    public static string Generate(
        IReadOnlyDictionary<string, string> parameters,
        string hashKey,
        string hashIv)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(hashKey))
        {
            throw new ArgumentException("HashKey must not be empty.", nameof(hashKey));
        }

        if (string.IsNullOrWhiteSpace(hashIv))
        {
            throw new ArgumentException("HashIv must not be empty.", nameof(hashIv));
        }

        // 1. 排除 CheckMacValue 本身，並按參數名稱字母順序（不區分大小寫）排序
        var sorted = parameters
            .Where(p => !string.Equals(p.Key, "CheckMacValue", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase);

        // 2. 串接成 query string，並以 HashKey 與 HashIV 夾住
        var query = string.Join("&", sorted.Select(p => $"{p.Key}={p.Value}"));
        var raw = $"HashKey={hashKey}&{query}&HashIV={hashIv}";

        // 3. URL Encode 並套用綠界 AIO 官方字元轉換表
        var encoded = HttpUtility.UrlEncode(raw);
        var normalized = encoded
            .Replace("%2d", "-", StringComparison.OrdinalIgnoreCase)
            .Replace("%5f", "_", StringComparison.OrdinalIgnoreCase)
            .Replace("%2e", ".", StringComparison.OrdinalIgnoreCase)
            .Replace("%21", "!", StringComparison.OrdinalIgnoreCase)
            .Replace("%2a", "*", StringComparison.OrdinalIgnoreCase)
            .Replace("%28", "(", StringComparison.OrdinalIgnoreCase)
            .Replace("%29", ")", StringComparison.OrdinalIgnoreCase);

        // 4. 轉為全小寫
        var lower = normalized.ToLowerInvariant();

        // 5. SHA256 雜湊
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(lower));

        // 6. 轉為全大寫十六進位字串
        return Convert.ToHexString(hashBytes).ToUpperInvariant();
    }

    public static bool Verify(
        IReadOnlyDictionary<string, string> parameters,
        string hashKey,
        string hashIv)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!parameters.TryGetValue("CheckMacValue", out var actualCheckMac) || string.IsNullOrWhiteSpace(actualCheckMac))
        {
            return false;
        }

        var expectedCheckMac = Generate(parameters, hashKey, hashIv);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedCheckMac.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(actualCheckMac.Trim().ToUpperInvariant()));
    }
}
