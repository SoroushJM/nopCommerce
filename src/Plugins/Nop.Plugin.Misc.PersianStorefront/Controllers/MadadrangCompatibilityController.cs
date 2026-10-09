using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core.Http;
using Nop.Services.Catalog;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

public sealed class MadadrangCompatibilityController(IProductService products, INopUrlHelper nopUrl) : BasePublicController
{
    [HttpGet]
    public IActionResult Cart()
    {
        return RedirectToRoute(NopRouteNames.General.CART);
    }

    [HttpGet]
    public async Task<IActionResult> Product(int id, int? updatecartitemid)
    {
        var product = await products.GetProductByIdAsync(id);
        if (product == null || product.Deleted)
            return NotFound();
        var url = await nopUrl.RouteGenericUrlAsync(product);
        if (updatecartitemid.HasValue)
            url = QueryHelpers.AddQueryString(url, "updatecartitemid", updatecartitemid.Value.ToString());
        return LocalRedirect(url);
    }
}