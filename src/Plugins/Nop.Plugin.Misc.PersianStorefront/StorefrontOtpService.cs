using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed partial class StorefrontOtpService(IWebHostEnvironment environment, IStorefrontOtpSender sender)
{
    private sealed class Challenge
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public string Phone = "";
        public byte[] Hash = [];
        public DateTimeOffset Sent;
        public int Attempts;
    }

    private readonly ConcurrentDictionary<Guid, Challenge> _challenges = new();

    public async Task<OtpResult> SendAsync(Guid guest, string raw)
    {
        if (!environment.IsDevelopment())
            return new(false, "ورود پیامکی هنوز به سرویس واقعی متصل نشده است.");
        var phone = Normalize(raw);
        if (!MobileNumber().IsMatch(phone))
            return new(false, "شمارهٔ موبایل را به شکل 09123456789 وارد کن.");
        var item = _challenges.GetOrAdd(guest, _ => new());
        await item.Gate.WaitAsync();
        try
        {
            if (DateTimeOffset.UtcNow - item.Sent < TimeSpan.FromSeconds(60))
                return new(false, "برای درخواست دوباره، یک دقیقه صبر کن.");
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var delivery = await sender.SendAsync(phone, code);
            if (!delivery.Success)
                return delivery;
            item.Phone = phone;
            item.Hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            item.Sent = DateTimeOffset.UtcNow;
            item.Attempts = 0;
            return delivery;
        }
        finally
        {
            item.Gate.Release();
        }
    }

    public async Task<string?> VerifyAsync(Guid guest, string raw, string code)
    {
        if (!environment.IsDevelopment() || !_challenges.TryGetValue(guest, out var item))
            return null;
        await item.Gate.WaitAsync();
        try
        {
            if (item.Phone != Normalize(raw) || item.Hash.Length == 0 || DateTimeOffset.UtcNow - item.Sent > TimeSpan.FromMinutes(2) || item.Attempts++ >= 5)
                return null;
            if (!CryptographicOperations.FixedTimeEquals(item.Hash, SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(code)))))
                return null;
            item.Hash = [];
            return item.Phone;
        }
        finally
        {
            item.Gate.Release();
        }
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        for (var i = 0; i < 10; i++)
            normalized = normalized.Replace((char)('۰' + i), (char)('0' + i)).Replace((char)('٠' + i), (char)('0' + i));
        if (normalized.StartsWith("+98", StringComparison.Ordinal))
            normalized = "0" + normalized[3..];
        return normalized;
    }

    [GeneratedRegex(@"^09[0-9]{9}$")]
    private static partial Regex MobileNumber();
}