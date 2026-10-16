using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nop.Services.Configuration;
using Nop.Services.Security;
using Nop.Services.Events;
using Nop.Core.Events;
using Nop.Data;

namespace Nop.Plugin.Sms.SmsIr;

public sealed class SmsIrLocalBootstrap(IConfiguration configuration, IWebHostEnvironment environment,
    IEncryptionService encryption, ISettingService settings)
{
    public async Task InitializeAsync()
    {
        if (!environment.IsDevelopment()) return;
        var current = await settings.LoadSettingAsync<SmsIrSettings>();
        if (!string.IsNullOrWhiteSpace(current.EncryptedApiKey)) return;
        var path = configuration["SmsIrBootstrap:SettingsPath"];
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var root = json.RootElement;
        var key = root.GetProperty("ApiKey").GetString();
        if (string.IsNullOrWhiteSpace(key)) return;
        current.EncryptedApiKey = encryption.EncryptText(key);
        current.Sandbox = root.TryGetProperty("Sandbox", out var sandbox) ? sandbox.GetBoolean() : true;
        current.TemplateId = root.TryGetProperty("TemplateId", out var template) ? template.GetInt32() : 123456;
        current.CodeParameterName = root.TryGetProperty("CodeParameterName", out var parameter) ? parameter.GetString() ?? "CODE" : "CODE";
        await settings.SaveSettingAsync(current);
    }
}

public sealed class SmsIrLocalBootstrapConsumer(SmsIrLocalBootstrap bootstrap, SmsIrRuntimeState runtime) : IConsumer<AppStartedEvent>
{
    public async Task HandleEventAsync(AppStartedEvent eventMessage)
    {
        if (!DataSettingsManager.IsDatabaseInstalled()) return;
        await bootstrap.InitializeAsync();
        await runtime.WriteAsync();
    }
}
