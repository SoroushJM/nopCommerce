using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Customers;
using Nop.Core.Infrastructure;
using Nop.Services.Common;
using Nop.Services.Themes;
using Nop.Web.Framework.Themes;

namespace Nop.Plugin.Misc.PersianStorefront;

// Deployment selection belongs to the process, not to shared database state.
public sealed class ConfiguredStorefrontThemeContext(IGenericAttributeService attributes, IStoreContext stores,
    IThemeProvider themes, IWorkContext work, StoreInformationSettings settings, StorefrontThemeConfig configuration)
    : ThemeContext(attributes, stores, themes, work, settings)
{
    public override async Task<string> GetWorkingThemeNameAsync()
    {
        if (string.IsNullOrWhiteSpace(configuration.Theme))
            return await base.GetWorkingThemeNameAsync();
        if (!string.IsNullOrEmpty(_cachedThemeName))
            return _cachedThemeName;
        if (!StorefrontThemeService.IsSupported(configuration.Theme) ||
            !await _themeProvider.ThemeExistsAsync(configuration.Theme))
            throw new InvalidOperationException($"Configured storefront theme '{configuration.Theme}' is not available in this deployment.");
        // Preserve the native customer selector when the merchant explicitly enables it.
        if (_storeInformationSettings.AllowCustomerToSelectTheme)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            if (customer != null)
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                var selected = await _genericAttributeService.GetAttributeAsync<string>(customer,
                    NopCustomerDefaults.WorkingThemeNameAttribute, store.Id);
                if (!string.IsNullOrEmpty(selected) && await _themeProvider.ThemeExistsAsync(selected))
                    return _cachedThemeName = selected;
            }
        }
        return _cachedThemeName = configuration.Theme;
    }
}

public sealed class StorefrontThemeStartup : INopStartup
{
    public int Order => 3002;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var selection = new StorefrontThemeConfig();
        configuration.GetSection(nameof(StorefrontThemeConfig)).Bind(selection);
        if (!string.IsNullOrWhiteSpace(selection.Theme) && !StorefrontThemeService.IsSupported(selection.Theme))
            throw new InvalidOperationException("StorefrontThemeConfig.Theme must be Madadrang or Boutique.");
        services.AddSingleton(selection);
        services.AddScoped<IThemeContext, ConfiguredStorefrontThemeContext>();
    }

    public void Configure(IApplicationBuilder application) { }
}
