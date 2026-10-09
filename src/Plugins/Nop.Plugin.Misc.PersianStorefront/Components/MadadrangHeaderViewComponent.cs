using Microsoft.AspNetCore.Mvc;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangHeaderViewComponent(StorefrontNavigation navigation, ICommonModelFactory common) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var header = await common.PrepareHeaderLinksModelAsync();
        var model = await navigation.PrepareAsync(header);
        HttpContext.Items["Madadrang.Navigation"] = model;
        ViewData["HeaderLinksModel"] = header;
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NativeHeader.cshtml", model);
    }
}