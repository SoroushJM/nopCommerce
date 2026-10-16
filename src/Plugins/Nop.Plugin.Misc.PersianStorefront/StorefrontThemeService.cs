using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Http;
using Nop.Services.Localization;
using Nop.Services.Themes;
using Nop.Web.Framework.Mvc.Routing;
using Storefront.UI.Shared;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontThemeConfig : IConfig
{
    public string Theme { get; set; } = "";
}

// Retain this type name because nopCommerce uses it in existing persisted setting keys.
public sealed class BoutiqueThemeSettings : ISettings
{
    public int HeroPictureId { get; set; }
    public string HeroHeading { get; set; } = "استایل تو، امضای تو";
    public string HeroDescription { get; set; } = "انتخاب‌های تازه برای استایل روزمرهٔ تو";
}

public sealed class StorefrontThemeService(IThemeContext themes, IStoreContext stores,
    ILocalizationService localization, INopUrlHelper urls, IWorkContext work)
{
    public static string NormalizeThemeName(string? theme) => theme?.Trim() switch
    {
        var value when string.Equals(value, "Boutique", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "MishaShop", StringComparison.OrdinalIgnoreCase) => "MishaShop",
        var value when string.Equals(value, "Madadrang", StringComparison.OrdinalIgnoreCase) => "Madadrang",
        var value => value ?? ""
    };

    public static bool IsSupported(string theme) => NormalizeThemeName(theme) is "Madadrang" or "MishaShop";

    public async Task<StorefrontThemeProfile> GetProfileAsync()
    {
        var theme = await themes.GetWorkingThemeNameAsync();
        var labels = await NativeUiLocalization.PrepareAsync(localization, work);
        if (theme == "MishaShop")
        {
            var store = await stores.GetCurrentStoreAsync();
            var name = await localization.GetLocalizedAsync(store, item => item.Name);
            return new StorefrontThemeProfile
            {
                Labels = labels,
                BrandName = string.IsNullOrWhiteSpace(name) ? "فروشگاه" : name,
                HomeUrl = urls.RouteUrl(NopRouteNames.General.HOMEPAGE),
                AccountUrl = urls.RouteUrl(NopRouteNames.General.CUSTOMER_INFO),
                LogoutUrl = urls.RouteUrl(NopRouteNames.Standard.LOGOUT),
                CatalogUrl = urls.RouteUrl("MadadrangAllProducts"),
                LoginUrl = urls.RouteUrl("StationeryLogin"), EnableOtp = true,
                OtpApiBasePath = urls.RouteUrl("StationeryLogin").Replace("/login", "/api/"),
                CartDescription = "انتخاب‌های تو", EmptyCartDescription = "لباس دلخواهت را پیدا کن.",
                SearchPlaceholder = "جست‌وجوی لباس و محصولات"
            };
        }

        return new StorefrontThemeProfile
        {
            Labels = labels,
            BrandName = "مداد رنگی",
            HomeUrl = urls.RouteUrl(NopRouteNames.General.HOMEPAGE),
            AccountUrl = urls.RouteUrl(NopRouteNames.General.CUSTOMER_INFO),
            LogoutUrl = urls.RouteUrl(NopRouteNames.Standard.LOGOUT),
            CatalogUrl = urls.RouteUrl("StationeryCatalog"),
            LoginUrl = urls.RouteUrl("StationeryLogin"),
            OtpApiBasePath = urls.RouteUrl("StationeryLogin").Replace("/login", "/api/"),
            EnableOtp = true,
            CartDescription = "یک جای کوچک برای انتخاب‌های رنگی",
            EmptyCartDescription = "برای شروع، یک همراه رنگی انتخاب کن.", HomeIcon = "pencil",
            SearchPlaceholder = "دنبال چی می‌گردی؟ دفتر، مداد، یک ایده تازه..."
        };
    }
}
