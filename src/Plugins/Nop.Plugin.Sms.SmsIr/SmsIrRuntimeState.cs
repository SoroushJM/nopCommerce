using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nop.Services.Configuration;
using Nop.Services.Security;

namespace Nop.Plugin.Sms.SmsIr;

// Deployment reads only this non-secret state; it never needs DB credentials or
// an admin cookie to detect unusable settings left by an upgrade.
public sealed class SmsIrRuntimeState(ISettingService settings, IEncryptionService encryption, IWebHostEnvironment environment)
{
    public async Task WriteAsync()
    {
        var current = await settings.LoadSettingAsync<SmsIrSettings>();
        var hasKey = !string.IsNullOrWhiteSpace(current.EncryptedApiKey);
        var warning = current.ConfigurationWarning(environment.IsDevelopment());
        if (hasKey)
        {
            try
            {
                var key = encryption.DecryptText(current.EncryptedApiKey);
                if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace)) warning = "کلید ذخیره‌شده قابل استفاده نیست.";
            }
            catch { warning = "کلید ذخیره‌شده با تنظیمات رمزنگاری فعلی قابل خواندن نیست."; }
        }
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "smsir-runtime-status.json");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new { HasKey = hasKey, Usable = warning is null, Warning = warning }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
