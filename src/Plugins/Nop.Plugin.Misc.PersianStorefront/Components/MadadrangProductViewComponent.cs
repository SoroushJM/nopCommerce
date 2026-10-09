using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangProductViewComponent : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(ProductDetailsModel model)
    {
        // The calling theme chooses its layout after this component has rendered.
        HttpContext.Items["Madadrang.NativePage"] = true;
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NativeProduct.cshtml", model);
    }
}