using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;
// Never registered as a real SMS provider. Controllers gate this behind Development.
public sealed partial class MockOtpStore
{
    private sealed class Challenge { public string Phone = ""; public byte[] Hash = []; public DateTimeOffset Sent; public int Attempts; }
    private readonly ConcurrentDictionary<Guid, Challenge> challenges = new();
    public OtpResult Send(Guid guest, string raw) { var phone = Normalize(raw); if (!MyRegex().IsMatch(phone)) return new(false, "شمارهٔ موبایل را به شکل 09123456789 وارد کن."); var item = challenges.GetOrAdd(guest, _ => new()); lock (item) { if (DateTimeOffset.UtcNow - item.Sent < TimeSpan.FromSeconds(60)) return new(false, "برای درخواست دوباره، یک دقیقه صبر کن."); var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(); item.Phone = phone; item.Hash = SHA256.HashData(Encoding.UTF8.GetBytes(code)); item.Sent = DateTimeOffset.UtcNow; item.Attempts = 0; return new(true, "کد آزمایشی آماده است.", code); } }
    public bool Verify(Guid guest, string raw, string code) { if (!challenges.TryGetValue(guest, out var item)) return false; lock (item) { if (item.Phone != Normalize(raw) || item.Hash.Length == 0 || DateTimeOffset.UtcNow - item.Sent > TimeSpan.FromMinutes(2) || item.Attempts++ >= 5) return false; if (!CryptographicOperations.FixedTimeEquals(item.Hash, SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code))))) return false; item.Hash = []; return true; } }
    public static string Normalize(string value) { var s = value.Trim(); for (var i = 0; i < 10; i++) s = s.Replace((char)('۰' + i), (char)('0' + i)).Replace((char)('٠' + i), (char)('0' + i)); if (s.StartsWith("+98")) s = "0" + s[3..]; return s; }

    [GeneratedRegex(@"^09\d{9}$")]
    private static partial Regex MyRegex();
}