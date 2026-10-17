using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Localization;

namespace Nop.Plugin.Misc.PersianStorefront;

/// <summary>Fills missing public Persian labels without changing merchant translations.</summary>
public sealed class PersianPublicResourcesStartup : INopStartup
{
    internal const string ResourceName = "Nop.Plugin.Misc.PersianStorefront.Localization.fa-IR.public.xml";

    // App_Data can be an older persistent container volume. Prefer the pack shipped
    // with this plugin revision while keeping file-only development hosts usable.
    internal static StreamReader? OpenResourcePack(string path)
    {
        var embedded = typeof(PersianPublicResourcesStartup).Assembly.GetManifestResourceStream(ResourceName);
        if (embedded != null)
            return new StreamReader(embedded);
        return File.Exists(path) ? File.OpenText(path) : null;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;
        using var scope = application.ApplicationServices.CreateScope();
        InstallAsync(scope.ServiceProvider).GetAwaiter().GetResult();
    }

    private static async Task InstallAsync(IServiceProvider services)
    {
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "Localization", "PersianPublic", "fa-IR.public.xml");
        using var reader = OpenResourcePack(path);
        if (reader == null)
        {
            services.GetRequiredService<ILogger<PersianPublicResourcesStartup>>()
                .LogWarning("The Persian public resource pack is missing at {Path}; existing translations remain available.", path);
            return;
        }

        var languages = await services.GetRequiredService<ILanguageService>().GetAllLanguagesAsync(true);
        var persian = languages.FirstOrDefault(language => language.LanguageCulture.Equals("fa-IR", StringComparison.OrdinalIgnoreCase));
        if (persian == null)
            return;

        var localization = services.GetRequiredService<ILocalizationService>();
        await PreserveRenamedWithdrawalResourceAsync(localization, persian.Id);
        await localization.ImportResourcesFromXmlAsync(persian, reader, updateExistingResources: false);
    }

    internal static async Task PreserveRenamedWithdrawalResourceAsync(ILocalizationService localization, int languageId)
    {
        const string oldName = "ReturnRequests.WithdrawalForm.ConfirnationText";
        const string newName = "ReturnRequests.WithdrawalForm.ConfirmationText";
        if (await localization.GetLocaleStringResourceByNameAsync(newName, languageId, logIfNotFound: false) != null)
            return;

        var previous = await localization.GetLocaleStringResourceByNameAsync(oldName, languageId, logIfNotFound: false);
        if (previous == null || string.IsNullOrWhiteSpace(previous.ResourceValue))
            return;

        // Keep the merchant's previous wording when upstream corrects the key's spelling.
        // Retain the old key for older integrations and never replace an existing new value.
        await localization.AddOrUpdateLocaleResourceAsync(
            new Dictionary<string, string> { [newName] = previous.ResourceValue }, languageId);
    }

    // The admin pack creates the language at 50; native culture setup follows at 100.
    public int Order => 51;
}
