using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Data;
using Nop.Services.Helpers;
using Storefront.UI.Shared;

namespace Nop.Plugin.Misc.PersianStorefront;

// One app process per isolated shop. Serialize short DB state changes, never HTTP.
public sealed class StorefrontOtpService(IWebHostEnvironment environment, IStorefrontOtpSender sender,
    IRepository<GenericAttribute> attributes, IWebHelper web, IStorefrontOtpAudit audit)
{
    private const string Group = "StorefrontOtp";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastCleanup;
    public sealed class Challenge
    {
        public string Phone { get; set; } = "";
        public string Hash { get; set; } = "";
        public string Salt { get; set; } = "";
        public string EnvironmentName { get; set; } = "";
        public DateTime Sent { get; set; }
        public int Attempts { get; set; }
        public bool Ready { get; set; }
    }
    public sealed class Rate
    {
        public DateTime Window { get; set; }
        public DateTime Last { get; set; }
        public int Count { get; set; }
    }
    private async Task<T?> ReadAsync<T>(string key) where T : class
    {
        var rows = await attributes.GetAllAsync(query => query.Where(a => a.KeyGroup == Group && a.EntityId == 0 && a.Key == key));
        return rows.Count == 1 ? JsonSerializer.Deserialize<T>(rows[0].Value) : null;
    }
    private async Task WriteAsync<T>(string key, T value)
    {
        var rows = await attributes.GetAllAsync(query => query.Where(a => a.KeyGroup == Group && a.EntityId == 0 && a.Key == key));
        if (rows.Count > 1) throw new InvalidOperationException("Duplicate OTP state records.");
        var row = rows.SingleOrDefault();
        if (row is null)
        {
            row = new GenericAttribute { EntityId = 0, KeyGroup = Group, Key = key, StoreId = 0, Value = JsonSerializer.Serialize(value), CreatedOrUpdatedDateUTC = DateTime.UtcNow };
            await attributes.InsertAsync(row, false);
        }
        else
        {
            row.Value = JsonSerializer.Serialize(value);
            row.CreatedOrUpdatedDateUTC = DateTime.UtcNow;
            await attributes.UpdateAsync(row, false);
        }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public async Task<OtpResult> SendAsync(Guid guest, string raw)
    {
        var phone = IranianMobile.Normalize(raw);
        if (guest == Guid.Empty || phone is null) return new(false, "شمارهٔ موبایل را به شکل 09123456789 وارد کن.");
        var now = DateTime.UtcNow;
        var guestKey = "guest-" + guest.ToString("N");
        var phoneKey = "phone-" + Hash(phone);
        var ipKey = "ip-" + Hash(web.GetCurrentIpAddress() ?? "unknown");
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var challenge = new Challenge { Phone = phone, Salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Sent = now, EnvironmentName = environment.EnvironmentName };
        challenge.Hash = Hash(challenge.Salt + code);
        await Gate.WaitAsync();
        try
        {
            if (now - _lastCleanup > TimeSpan.FromHours(1))
            {
                var cutoff = now.AddDays(-1);
                await attributes.DeleteAsync(a => a.KeyGroup == Group && a.CreatedOrUpdatedDateUTC < cutoff);
                _lastCleanup = now;
            }
            var old = await ReadAsync<Challenge>(guestKey);
            var phoneRate = await ReadAsync<Rate>(phoneKey) ?? new Rate { Window = now };
            var ipRate = await ReadAsync<Rate>(ipKey) ?? new Rate { Window = now };
            if (now - phoneRate.Window >= TimeSpan.FromMinutes(15)) phoneRate = new() { Window = now, Last = phoneRate.Last };
            if (now - ipRate.Window >= TimeSpan.FromHours(1)) ipRate = new() { Window = now };
            if ((old is not null && now - old.Sent < TimeSpan.FromSeconds(60)) || now - phoneRate.Last < TimeSpan.FromSeconds(60))
            {
                var last = old is not null && old.Sent > phoneRate.Last ? old.Sent : phoneRate.Last;
                await audit.BlockedAsync(phone, old is not null && old.Sent > phoneRate.Last ? "GuestCooldown" : "PhoneCooldown", last.AddSeconds(60));
                return new(false, "برای درخواست دوباره، یک دقیقه صبر کن.");
            }
            if (phoneRate.Count >= 5 || ipRate.Count >= 20)
            {
                await audit.BlockedAsync(phone, phoneRate.Count >= 5 ? "PhoneRateLimit" : "IpRateLimit",
                    phoneRate.Count >= 5 ? phoneRate.Window.AddMinutes(15) : ipRate.Window.AddHours(1));
                return new(false, "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.");
            }
            phoneRate.Count++; phoneRate.Last = now;
            ipRate.Count++; ipRate.Last = now;
            // Reserve before HTTP, including failed/ambiguous sends.
            await WriteAsync(phoneKey, phoneRate);
            await WriteAsync(ipKey, ipRate);
            await WriteAsync(guestKey, challenge);
        }
        finally { Gate.Release(); }
        var delivery = await sender.SendAsync(phone, code);
        await Gate.WaitAsync();
        try
        {
            var current = await ReadAsync<Challenge>(guestKey);
            if (current?.Salt == challenge.Salt)
            {
                current.Ready = delivery.Success;
                if (!delivery.Success) current.Hash = "";
                await WriteAsync(guestKey, current);
            }
        }
        finally { Gate.Release(); }
        return delivery with { TestCode = environment.IsDevelopment() ? delivery.TestCode : null };
    }

    public async Task<string?> VerifyAsync(Guid guest, string raw, string code)
    {
        await Gate.WaitAsync();
        try
        {
            var key = "guest-" + guest.ToString("N");
            var item = await ReadAsync<Challenge>(key);
            if (item is null || item.EnvironmentName != environment.EnvironmentName || !item.Ready || item.Hash.Length == 0 || DateTime.UtcNow - item.Sent > TimeSpan.FromMinutes(2) || item.Attempts >= 5) return null;
            item.Attempts++;
            var matches = item.Phone == IranianMobile.Normalize(raw) && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(item.Hash), Convert.FromHexString(Hash(item.Salt + IranianMobile.Digits(code))));
            if (matches || item.Attempts >= 5) item.Hash = "";
            await WriteAsync(key, item);
            return matches ? item.Phone : null;
        }
        finally { Gate.Release(); }
    }
}
