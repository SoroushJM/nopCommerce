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
        if (!File.Exists(path))
        {
            services.GetRequiredService<ILogger<PersianPublicResourcesStartup>>()
                .LogWarning("The Persian public resource pack is missing at {Path}; existing translations remain available.", path);
            return;
        }

        var languages = await services.GetRequiredService<ILanguageService>().GetAllLanguagesAsync(true);
        var persian = languages.FirstOrDefault(language => language.LanguageCulture.Equals("fa-IR", StringComparison.OrdinalIgnoreCase));
        if (persian == null)
            return;

        using var reader = File.OpenText(path);
        await services.GetRequiredService<ILocalizationService>()
            .ImportResourcesFromXmlAsync(persian, reader, updateExistingResources: false);
    }

    // The admin pack creates the language at 50; native culture setup follows at 100.
    public int Order => 51;
}
