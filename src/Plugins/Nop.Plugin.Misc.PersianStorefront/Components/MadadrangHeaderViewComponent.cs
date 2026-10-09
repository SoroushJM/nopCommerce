using Microsoft.AspNetCore.Mvc;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangHeaderViewComponent(StorefrontNavigation navigation, ICommonModelFactory common) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = await navigation.PrepareAsync();
        HttpContext.Items["Madadrang.Navigation"] = model;
        ViewData["HeaderLinksModel"] = await common.PrepareHeaderLinksModelAsync();
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NativeHeader.cshtml", model);
    }
}
