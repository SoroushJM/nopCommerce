using System.Net.Http.Json;
using System.Text.Json;

namespace Nop.Plugin.Sms.SmsIr;

public sealed record SmsIrSendResult(bool Success, string Message, int? ProviderStatus = null, long? MessageId = null, bool IsAmbiguous = false, bool IsBlacklisted = false);
public sealed record SmsIrDeliveryResult(bool Success, int? Status = null);

// No automatic retries: retrying an ambiguous POST can send and charge twice.
public sealed class SmsIrClient(HttpClient http)
{
    public Task<SmsIrSendResult> SendCodeAsync(string key, int templateId, string parameter, string phone, string code,
        CancellationToken cancellationToken = default) => SendAsync("send/verify", key, new
        {
            mobile = phone,
            templateId,
            parameters = new[] { new { name = parameter, value = code } }
        }, cancellationToken);

    public Task<SmsIrSendResult> SendTextAsync(string key, long lineNumber, string phone, string text,
        CancellationToken cancellationToken = default) => SendAsync("send/bulk", key, new
        {
            lineNumber, messageText = text, mobiles = new[] { phone }
        }, cancellationToken);

    private async Task<SmsIrSendResult> SendAsync(string path, string key, object body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://api.sms.ir/v1/" + path));
            request.Headers.Add("X-API-KEY", key);
            request.Content = JsonContent.Create(body);
            using var response = await http.SendAsync(request, cancellationToken);
            // Never expose the remote body, request headers, recipient or verification code.
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.BadRequest)
                return new(false, response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "کلید API سرویس پیامک معتبر نیست؛ مدیر فروشگاه باید تنظیمات را بررسی کند.",
                    System.Net.HttpStatusCode.TooManyRequests => "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.",
                    _ => "سرویس پیامک در دسترس نیست؛ کمی بعد دوباره تلاش کنید."
                }, IsAmbiguous: (int)response.StatusCode >= 500);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Number ||
                !status.TryGetInt32(out var value))
                return new(false, "پاسخ سرویس پیامک معتبر نیست.", IsAmbiguous: true);
            long? messageId = null;
            var blacklisted = value == 115;
            var invalidRecipient = false;
            if (json.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("messageId", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number) && number > 0) messageId = number;
                else if (data.TryGetProperty("messageIds", out var ids) && ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() == 1)
                {
                    if (ids[0].ValueKind == JsonValueKind.Number && ids[0].TryGetInt64(out number))
                    { if (number > 0) messageId = number; else if (number == 0) blacklisted = true; }
                    else if (ids[0].ValueKind == JsonValueKind.Null) invalidRecipient = true;
                }
            }
            if (blacklisted) return new(false, "شماره در لیست سیاه سرویس پیامک است.", value, IsBlacklisted: true);
            if (invalidRecipient) return new(false, "سرویس، پیامک این گیرنده را نپذیرفت.", value);
            if (value == 1 && (!response.IsSuccessStatusCode || messageId is null))
                return new(false, "تأیید قطعی ارسال و شناسهٔ پیام دریافت نشد.", value, IsAmbiguous: response.IsSuccessStatusCode);
            return (value switch
            {
                1 => new SmsIrSendResult(true, "کد ورود ارسال شد."),
                20 => new(false, "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید."),
                102 => new(false, "اعتبار سرویس پیامک کافی نیست؛ با پشتیبانی فروشگاه تماس بگیرید."),
                113 or 119 => new(false, "قالب پیامک آماده نیست؛ مدیر فروشگاه باید تنظیمات را بررسی کند."),
                _ => new(false, "ارسال پیامک انجام نشد؛ کمی بعد دوباره تلاش کنید.")
            }) with { ProviderStatus = value, MessageId = messageId };
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new(false, "پاسخ قطعی ارسال دریافت نشد؛ کمی بعد دوباره تلاش کنید.", IsAmbiguous: true);
        }
    }

    public async Task<SmsIrDeliveryResult> GetDeliveryAsync(string key, long messageId, CancellationToken cancellationToken = default)
    {
        if (messageId <= 0) return new(false);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.sms.ir/v1/send/" + messageId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.Add("X-API-KEY", key);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return new(false);
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.Number || !status.TryGetInt32(out var value) || value != 1 ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("messageId", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var returnedId) || returnedId != messageId)
                return new(false);
            if (!data.TryGetProperty("deliveryState", out var delivery) || delivery.ValueKind == JsonValueKind.Null) return new(true);
            return delivery.ValueKind == JsonValueKind.Number && delivery.TryGetInt32(out var state) && state is >= 1 and <= 7 ? new(true, state) : new(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException) { return new(false); }
    }
}
