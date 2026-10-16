using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nop.Core.Domain.Cms;
using Nop.Core.Domain.Messages;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrPlugin(ISettingService settings, WidgetSettings widgets, MessagesSettings messages,
    SmsIrSettings sms, SmsIrClient client, IEncryptionService encryption, IWebHostEnvironment environment,
    INopUrlHelper urls, SmsIrLocalBootstrap bootstrap, ISmsIrJournal journal, SmsIrRuntimeState runtime) : BasePlugin, ISmsProvider, IWidgetPlugin
{
    public const string SystemName = "Sms.SmsIr";
    public const string ConfigurationRoute = "Plugin.SmsIr.Configure";
    public override string GetConfigurationPageUrl() => urls.RouteUrl(ConfigurationRoute);
    public bool HideInWidgetList => true;
    public Task<IList<string>> GetWidgetZonesAsync() => Task.FromResult<IList<string>>([AdminWidgetZones.DashboardTop]);
    public Type GetWidgetViewComponent(string widgetZone) => typeof(SmsIrWarningViewComponent);

    public async Task<bool> SendSmsAsync(string phone, string text)
    {
        var canonical = Nop.Plugin.Misc.PersianStorefront.IranianMobile.Normalize(phone);
        if (canonical is null) return false;
        var entry = await journal.BeginAsync(canonical, "Text", 0);
        // The native interface carries arbitrary text. Never extract OTP from a localized sentence.
        if (sms.ConfigurationWarning(environment.IsDevelopment()) is not null || sms.LineNumber <= 0)
        {
            await journal.CompleteAsync(entry, new(false, "تنظیمات ارسال متن عمومی آماده نیست."));
            return false;
        }
        try
        {
            var result = await client.SendTextAsync(encryption.DecryptText(sms.EncryptedApiKey), sms.LineNumber, canonical, text);
            await journal.CompleteAsync(entry, result);
            return result.Success;
        }
        catch { await journal.CompleteAsync(entry, new(false, "ارسال متن عمومی انجام نشد.")); return false; }
    }

    public override async Task InstallAsync()
    {
        // Preserve settings left by an earlier installation or an administrator.
        if (!await settings.SettingExistsAsync(sms, s => s.Sandbox))
            await settings.SaveSettingAsync(new SmsIrSettings());
        if (!widgets.ActiveWidgetSystemNames.Contains(SystemName))
        {
            widgets.ActiveWidgetSystemNames.Add(SystemName);
            await settings.SaveSettingAsync(widgets);
        }
        messages.ActiveSmsProviderSystemName = SystemName;
        await settings.SaveSettingAsync(messages);
        await bootstrap.InitializeAsync();
        await runtime.WriteAsync();
        await base.InstallAsync();
    }
}
