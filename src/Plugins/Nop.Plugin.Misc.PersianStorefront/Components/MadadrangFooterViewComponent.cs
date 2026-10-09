using Microsoft.AspNetCore.Mvc;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangFooterViewComponent(StorefrontNavigation navigation, ICommonModelFactory common) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = HttpContext.Items["Madadrang.Navigation"] as StoreNavigation ?? await navigation.PrepareAsync();
        ViewData["FooterModel"] = await common.PrepareFooterModelAsync();
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NativeFooter.cshtml", model);
    }
}