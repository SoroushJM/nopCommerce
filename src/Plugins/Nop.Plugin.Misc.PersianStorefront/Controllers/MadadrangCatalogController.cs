using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront.Controllers;

public sealed class MadadrangCatalogController(NativeCatalogModelFactory factory, ICategoryService categories,
    IStoreContext stores, ILocalizationService localization, INopUrlHelper nopUrl) : BasePublicController
{
    [HttpGet]
    [SaveLastContinueShoppingPage]
    public async Task<IActionResult> AllProducts(CatalogProductsCommand command, string? q, int cid = 0, bool isc = true)
    {
        var model = await factory.PrepareAsync(command, q, cid, isc);
        if (model == null)
            return NotFound();
        return View("~/Plugins/Misc.PersianStorefront/Views/AllProducts.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> LegacyCatalog(string? category, string? sort)
    {
        var query = new Dictionary<string, string?>();
        foreach (var key in new[] { "q", "cid", "isc", "orderby", "pagesize", "pagenumber", "viewmode", "price", "ms", "specs" })
        {
            if (Request.Query.TryGetValue(key, out var value))
                query[key] = value.ToString();
        }
        if (!query.ContainsKey("orderby"))
        {
            var order = sort switch
            {
                "price" => "10",
                "price-desc" => "11",
                "popular" => "0",
                _ => null
            };
            if (order != null)
                query["orderby"] = order;
        }
        var destination = Url.RouteUrl("MadadrangAllProducts")!;
        if (!string.IsNullOrWhiteSpace(category) && !query.ContainsKey("cid"))
        {
            var matches = new List<Nop.Core.Domain.Catalog.Category>();
            foreach (var item in await categories.GetAllCategoriesAsync((await stores.GetCurrentStoreAsync()).Id))
            {
                if (string.Equals(item.Name, category, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(await localization.GetLocalizedAsync(item, value => value.Name), category, StringComparison.OrdinalIgnoreCase))
                    matches.Add(item);
            }
            if (matches.Count != 1)
                return NotFound();
            if (string.IsNullOrWhiteSpace(query.GetValueOrDefault("q")))
                destination = await nopUrl.RouteGenericUrlAsync(matches[0]);
            else
                query["cid"] = matches[0].Id.ToString();
        }
        return LocalRedirect(QueryHelpers.AddQueryString(destination, query));
    }
}