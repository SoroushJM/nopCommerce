using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangProductViewComponent(IProductService products, INopUrlHelper nopUrl,
    ILocalizationService localization, IWorkContext workContext) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(ProductDetailsModel model)
    {
        // The calling theme chooses its layout after this component has rendered.
        HttpContext.Items["Madadrang.NativePage"] = true;
        var product = await products.GetProductByIdAsync(model.Id);
        ViewData["NativeProduct"] = await NativeProductPresentation.FromAsync(model, Url, nopUrl, localization,
            workContext, product?.OrderMinimumQuantity ?? 1);
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NativeProduct.cshtml", model);
    }
}