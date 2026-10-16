using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nop.Plugin.Misc.PersianStorefront;
using Nop.Services.Security;
using Storefront.UI.Shared;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrSender(SmsIrSettings settings, SmsIrClient client, IEncryptionService encryption,
    IWebHostEnvironment environment, ISmsIrJournal journal) : IStorefrontOtpSender
{
    public async Task<OtpResult> SendAsync(string phone, string code)
    {
        var entry = await journal.BeginAsync(phone, "OTP", settings.TemplateId);
        if (settings.ConfigurationWarning(environment.IsDevelopment()) is not null)
        {
            await journal.CompleteAsync(entry, new(false, "تنظیمات ارسال آماده نیست."));
            return new(false, "ورود پیامکی آماده نیست؛ با پشتیبانی فروشگاه تماس بگیرید.");
        }
        string key;
        try { key = encryption.DecryptText(settings.EncryptedApiKey); }
        catch
        {
            await journal.CompleteAsync(entry, new(false, "کلید رمزنگاری‌شده قابل خواندن نیست."));
            return new(false, "تنظیمات سرویس پیامک قابل خواندن نیست؛ با پشتیبانی تماس بگیرید.");
        }
        var result = await client.SendCodeAsync(key, settings.TemplateId, settings.CodeParameterName, phone, code);
        await journal.CompleteAsync(entry, result);
        return new(result.Success,
            result.Success && settings.Sandbox ? "درخواست آزمایشی پذیرفته شد." : result.Message,
            result.Success && settings.Sandbox && environment.IsDevelopment() ? code : null);
    }
}
