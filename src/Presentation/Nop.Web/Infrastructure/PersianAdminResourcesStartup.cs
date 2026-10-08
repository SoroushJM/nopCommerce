using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Nop.Core.Caching;
using Nop.Core.Domain.Localization;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Common;
using Nop.Services.Localization;

namespace Nop.Web.Infrastructure;

/// <summary>Installs the versioned admin pack without replacing translations edited by an administrator.</summary>
public sealed class PersianAdminResourcesStartup : INopStartup
{
    private const string LedgerAttribute = "PersianAdminResourceBaseline";

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }

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
        // The recovery switch must also rescue startup if a deployed Persian pack is malformed.
        var recoveryFile = Path.Combine(environment.ContentRootPath, "App_Data", "admin-language.override.json");
        if (File.Exists(recoveryFile))
        {
            try
            {
                using var recovery = JsonDocument.Parse(await File.ReadAllTextAsync(recoveryFile));
                if (recovery.RootElement.TryGetProperty("ForceEnglish", out var forced) && forced.ValueKind == JsonValueKind.True)
                    return;
            }
            catch (JsonException) { return; }
            catch (IOException) { return; }
        }
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "Localization", "PersianAdmin", "fa-IR.admin.xml");
        if (!File.Exists(path))
        {
            services.GetRequiredService<ILogger<PersianAdminResourcesStartup>>()
                .LogWarning("The Persian administrator resource pack is missing at {Path}; existing languages remain available.", path);
            return;
        }

        var languageService = services.GetRequiredService<ILanguageService>();
        var languages = await languageService.GetAllLanguagesAsync(true);
        var persian = languages.FirstOrDefault(language => language.LanguageCulture.Equals("fa-IR", StringComparison.OrdinalIgnoreCase));
        if (persian == null)
        {
            persian = new Language
            {
                Name = "فارسی", LanguageCulture = "fa-IR", UniqueSeoCode = "fa",
                FlagImageFileName = "ir.png", Published = true, Rtl = true,
                DisplayOrder = languages.Count == 0 ? 1 : languages.Max(language => language.DisplayOrder) + 1
            };
            await languageService.InsertLanguageAsync(persian);
        }
        else if (!persian.Rtl || !persian.Published)
        {
            persian.Rtl = true;
            persian.Published = true;
            await languageService.UpdateLanguageAsync(persian);
        }

        // GenericAttribute.Value supports a complete pack ledger; Setting.Value is limited to 6000 characters.
        var attributes = services.GetRequiredService<IGenericAttributeService>();
        var previousJson = await attributes.GetAttributeAsync<string>(persian, LedgerAttribute, defaultValue: "{}");
        var previous = JsonSerializer.Deserialize<Dictionary<string, string>>(previousJson)
            ?? new Dictionary<string, string>();
        var repository = services.GetRequiredService<IRepository<LocaleStringResource>>();
        var english = languages.FirstOrDefault(language => language.LanguageCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        var englishValues = english == null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : (await repository.GetAllAsync(query => query.Where(resource => resource.LanguageId == english.Id)))
                .GroupBy(resource => resource.ResourceName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().ResourceValue, StringComparer.OrdinalIgnoreCase);
        var englishPack = Path.Combine(Path.GetDirectoryName(path)!, "en-US.admin.xml");
        if (english != null && File.Exists(englishPack))
        {
            var englishInserts = XDocument.Load(englishPack).Root!.Elements("LocaleResource")
                .Where(element => !englishValues.ContainsKey(element.Attribute("Name")!.Value))
                .Select(element => new LocaleStringResource
                {
                    LanguageId = english.Id, ResourceName = element.Attribute("Name")!.Value,
                    ResourceValue = element.Element("Value")!.Value
                }).ToList();
            if (englishInserts.Count > 0)
            {
                await repository.InsertAsync(englishInserts);
                await services.GetRequiredService<IStaticCacheManager>().RemoveByPrefixAsync("Nop.localestringresource.");
            }
        }
        var existing = (await repository.GetAllAsync(query => query.Where(resource => resource.LanguageId == persian.Id)))
            .GroupBy(resource => resource.ResourceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var baseline = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inserts = new List<LocaleStringResource>();
        var updates = new List<LocaleStringResource>();
        foreach (var element in XDocument.Load(path).Root!.Elements("LocaleResource"))
        {
            var name = element.Attribute("Name")!.Value;
            var value = element.Element("Value")!.Value;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
            baseline[name] = hash;
            if (!existing.TryGetValue(name, out var resource))
                inserts.Add(new LocaleStringResource { LanguageId = persian.Id, ResourceName = name, ResourceValue = value });
            else if (resource.ResourceValue != value &&
                ((previous.TryGetValue(name, out var priorHash)
                    && priorHash == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resource.ResourceValue))))
                 || (previous.Count == 0 && englishValues.TryGetValue(name, out var englishValue)
                    && resource.ResourceValue == englishValue)))
            {
                resource.ResourceValue = value;
                updates.Add(resource);
            }
            // First deployment may replace a copied English seed; other unmatched values belong to the administrator.
        }
        if (inserts.Count > 0)
            await repository.InsertAsync(inserts);
        if (updates.Count > 0)
            await repository.UpdateAsync(updates);
        var json = JsonSerializer.Serialize(baseline);
        if (json != previousJson)
            await attributes.SaveAttributeAsync(persian, LedgerAttribute, json);
        if (inserts.Count > 0 || updates.Count > 0)
            await services.GetRequiredService<IStaticCacheManager>().RemoveByPrefixAsync("Nop.localestringresource.");
    }

    // Run before the framework builds its supported culture list (order 100).
    public int Order => 50;
}
