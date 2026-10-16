using Nop.Core.Configuration;
using System.Text.RegularExpressions;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrSettings : ISettings
{
    public string EncryptedApiKey { get; set; } = "";
    public bool Sandbox { get; set; } = true;
    public int TemplateId { get; set; } = 123456;
    public string CodeParameterName { get; set; } = "CODE";
    // Optional native free-text SMS transport. Storefront OTP always uses Verify.
    public long LineNumber { get; set; }

    public string? ConfigurationWarning(bool development)
    {
        if (string.IsNullOrWhiteSpace(EncryptedApiKey))
            return "ورود پیامکی آماده نیست؛ کلید API سرویس sms.ir را در تنظیمات افزونه وارد کنید.";
        if (TemplateId <= 0 || !Regex.IsMatch(CodeParameterName ?? "", "^[A-Za-z][A-Za-z0-9_]{0,39}$"))
            return "ورود پیامکی آماده نیست؛ شناسهٔ قالب و نام پارامتر کد را تنظیم کنید.";
        if (Sandbox && !development)
            return "sms.ir برای Sandbox تنظیم شده است؛ ورود پیامکی در این محیط غیرفعال است.";
        return null;
    }
}
