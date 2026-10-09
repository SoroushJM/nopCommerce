using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Tax;
using Nop.Core.Events;
using Nop.Core.Infrastructure;
using Nop.Core.Security;
using Nop.Data;
using Nop.Services.Authentication;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Vendors;
using Nop.Web.Framework;

namespace Nop.Plugin.Misc.PersianStorefront;

/// <summary>Admin preferences never modify Customer.LanguageId or the public culture cookie.</summary>
public sealed class AdminLanguageWorkContext : WebWorkContext
{
    public const string PreferenceKey = "PersianAdmin.LanguageId";
    public const string CookieName = ".Nop.AdminLanguage";
    private readonly IWebHostEnvironment _environment;

    public AdminLanguageWorkContext(CookieSettings cookies, CurrencySettings currencies,
        IAuthenticationService authentication, ICurrencyService currencyService,
        ICustomerService customers, IEventPublisher events, IGenericAttributeService attributes,
        IHttpContextAccessor accessor, ILanguageService languages, IStoreContext stores,
        IUserAgentHelper userAgent, IVendorService vendors, IWebHelper web,
        LocalizationSettings localization, TaxSettings tax, IWebHostEnvironment environment)
        : base(cookies, currencies, authentication, currencyService, customers, events,
            attributes, accessor, languages, stores, userAgent, vendors, web, localization, tax)
    {
        _environment = environment;
    }

    public static bool IsAdminRequest(HttpRequest? request)
    {
        if (request == null)
            return false;
        if (request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
            return true;
        // The native login remains available for administrators without changing customer login.
        return request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase) &&
            request.Query["returnUrl"].ToString().StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
    }

    private bool ForceEnglish()
    {
        var file = Path.Combine(_environment.ContentRootPath, "App_Data", "admin-language.override.json");
        if (!File.Exists(file))
            return false;
        // An invalid recovery file must not silently make the admin inaccessible.
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            return json.RootElement.TryGetProperty("ForceEnglish", out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return true; }
        catch (IOException) { return true; }
    }

    public override async Task<Language> GetWorkingLanguageAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null || !IsAdminRequest(context.Request))
            return await base.GetWorkingLanguageAsync();
        if (_cachedLanguage != null)
            return _cachedLanguage;
        var languages = await _languageService.GetAllLanguagesAsync();
        var english = languages.FirstOrDefault(x => x.LanguageCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        if (ForceEnglish())
        {
            return _cachedLanguage = english ?? languages.FirstOrDefault()
            ?? throw new InvalidOperationException("No published administration language is available.");
        }

        var customer = await GetCurrentCustomerAsync();
        var id = await _genericAttributeService.GetAttributeAsync<int>(customer, PreferenceKey);
        if (id == 0 && int.TryParse(context.Request.Cookies[CookieName], out var cookieId))
            id = cookieId;
        return _cachedLanguage = languages.FirstOrDefault(x => x.Id == id)
            ?? languages.FirstOrDefault(x => x.LanguageCulture.Equals("fa-IR", StringComparison.OrdinalIgnoreCase))
            ?? english ?? languages.FirstOrDefault()
            ?? throw new InvalidOperationException("No published administration language is available.");
    }

    public override async Task SetWorkingLanguageAsync(Language language)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null || !IsAdminRequest(context.Request))
        {
            await base.SetWorkingLanguageAsync(language);
            return;
        }
        if (language == null)
            return;
        var customer = await GetCurrentCustomerAsync();
        if (!customer.IsSystemAccount)
            await _genericAttributeService.SaveAttributeAsync(customer, PreferenceKey, language.Id);
        context.Response.Cookies.Append(CookieName, language.Id.ToString(CultureInfo.InvariantCulture), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = context.Request.PathBase.HasValue ? context.Request.PathBase.Value + "/" : "/",
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true
        });
        _cachedLanguage = null;
    }
}

public sealed class AdminFallbackLocalizationService : LocalizationService
{
    private readonly IHttpContextAccessor _accessor;
    public AdminFallbackLocalizationService(ILanguageService languages, ILocalizedEntityService entities,
        ILogger logger, IRepository<LocaleStringResource> resources, ISettingService settings,
        IStaticCacheManager cache, IWorkContext context, LocalizationSettings localization,
        IHttpContextAccessor accessor) : base(languages, entities, logger, resources, settings, cache, context, localization)
    {
        _accessor = accessor;
    }

    public override async Task<string> GetResourceAsync(string resourceKey, int languageId,
        bool logIfNotFound = true, string defaultValue = "", bool returnEmptyIfNotFound = false)
    {
        if (!AdminLanguageWorkContext.IsAdminRequest(_accessor.HttpContext?.Request))
            return await base.GetResourceAsync(resourceKey, languageId, logIfNotFound, defaultValue, returnEmptyIfNotFound);
        var translated = await base.GetResourceAsync(resourceKey, languageId, false, "", true);
        if (!string.IsNullOrEmpty(translated))
            return translated;
        var language = await _languageService.GetLanguageByIdAsync(languageId);
        if (language?.LanguageCulture.StartsWith("fa", StringComparison.OrdinalIgnoreCase) == true)
        {
            var english = (await _languageService.GetAllLanguagesAsync(showHidden: true))
                .FirstOrDefault(x => x.LanguageCulture.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            if (english != null)
            {
                var fallback = await base.GetResourceAsync(resourceKey, english.Id, false, "", true);
                if (!string.IsNullOrEmpty(fallback))
                    return fallback;
            }
        }
        return await base.GetResourceAsync(resourceKey, languageId, logIfNotFound, defaultValue, returnEmptyIfNotFound);
    }
}

// Runs after native localization and before routing/authentication, on admin requests only.
public sealed class AdminCultureStartup : INopStartup
{
    public int Order => 110;
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }
    public void Configure(IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
    {
        if (DataSettingsManager.IsDatabaseInstalled() && AdminLanguageWorkContext.IsAdminRequest(context.Request))
        {
            var language = await context.RequestServices.GetRequiredService<IWorkContext>().GetWorkingLanguageAsync();
            if (language != null)
            {
                var culture = new CultureInfo(language.LanguageCulture);
                culture.DateTimeFormat.Calendar = new GregorianCalendar();
                // Internal model binding remains invariant; Persian visible editors normalize their backing values.
                culture.NumberFormat = (NumberFormatInfo)CultureInfo.GetCultureInfo("en-US").NumberFormat.Clone();
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                context.Response.Headers.ContentLanguage = culture.Name;
            }
        }
        await next();
    });
    }
}

public sealed class AdminLanguageRegistration : INopStartup
{
    public int Order => 3000;
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IWorkContext, AdminLanguageWorkContext>();
        services.AddScoped<ILocalizationService, AdminFallbackLocalizationService>();
    }
    public void Configure(IApplicationBuilder app)
    {
    }
}