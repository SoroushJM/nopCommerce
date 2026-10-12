using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;
using Storefront.UI.Shared;
using Nop.Core.Domain;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class BoutiqueHeaderViewComponent(StorefrontNavigation navigation, ICommonModelFactory common,
    StorefrontThemeService theme, StoreInformationSettings storeSettings) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var header = await common.PrepareHeaderLinksModelAsync();
        var model = await navigation.PrepareAsync(header);
        HttpContext.Items["Storefront.Navigation"] = model;
        ViewData["HeaderLinksModel"] = header;
        ViewData["ThemeProfile"] = await theme.GetProfileAsync();
        if (storeSettings.LogoPictureId > 0)
            ViewData["LogoUrl"] = (await common.PrepareLogoModelAsync()).LogoPath;
        return await ViewAsync("~/Themes/Boutique/Views/Shared/Components/BoutiqueHeader/Default.cshtml", model);
    }
}

public sealed class BoutiqueFooterViewComponent(StorefrontNavigation navigation, ICommonModelFactory common,
    StorefrontThemeService theme) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = HttpContext.Items["Storefront.Navigation"] as StoreNavigation ?? await navigation.PrepareAsync();
        ViewData["FooterModel"] = await common.PrepareFooterModelAsync();
        ViewData["ThemeProfile"] = await theme.GetProfileAsync();
        return await ViewAsync("~/Themes/Boutique/Views/Shared/Components/BoutiqueFooter/Default.cshtml", model);
    }
}

public sealed class BoutiqueHeroViewComponent(IStoreContext stores, ISettingService settings,
    IPictureService pictures, StorefrontThemeService theme) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await stores.GetCurrentStoreAsync();
        var configuration = await settings.LoadSettingAsync<BoutiqueThemeSettings>(store.Id);
        var profile = await theme.GetProfileAsync();
        ViewData["BrandName"] = profile.BrandName;
        ViewData["CatalogUrl"] = profile.CatalogUrl;
        ViewData["ImageUrl"] = configuration.HeroPictureId > 0
            ? await pictures.GetPictureUrlAsync(configuration.HeroPictureId, 1200, showDefaultPicture: false) : "";
        ViewData["ImageAlt"] = profile.BrandName;
        ViewData["Heading"] = configuration.HeroHeading;
        ViewData["Description"] = configuration.HeroDescription;
        return await ViewAsync("~/Themes/Boutique/Views/Shared/Components/BoutiqueHero/Default.cshtml");
    }
}
