using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangCatalogControlsViewComponent(IWorkContext work) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(CatalogProductsModel model, string placement, SearchModel? search = null, bool allProducts = false)
    {
        var controls = new CatalogControls
        {
            Path = HttpContext.Request.PathBase + HttpContext.Request.Path,
            NativeSearch = search != null && !allProducts,
            SortOptions = model.AllowProductSorting ? Choices(model.AvailableSortOptions) : [],
            PageSizes = model.AllowCustomersToSelectPageSize ? Choices(model.PageSizeOptions) : [],
            ViewModes = model.AllowProductViewModeChanging ? Choices(model.AvailableViewModes) : [],
            PriceEnabled = model.PriceRangeFilter.Enabled,
            PriceFrom = Price(model.PriceRangeFilter.SelectedPriceRange.From),
            PriceTo = Price(model.PriceRangeFilter.SelectedPriceRange.To),
            AvailableFrom = Price(model.PriceRangeFilter.AvailablePriceRange.From),
            AvailableTo = Price(model.PriceRangeFilter.AvailablePriceRange.To),
            Currency = (await work.GetWorkingCurrencyAsync()).Name,
            DecimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator
        };
        foreach (var key in new[] { "q", "advs", "cid", "isc", "mid", "vid", "sid", "sit", "price", "orderby", "pagesize", "viewmode" })
        {
            if (HttpContext.Request.Query.TryGetValue(key, out var value))
                controls.Query[key] = value.ToString();
        }
        var sort = model.AvailableSortOptions.FirstOrDefault(option => option.Selected)?.Value;
        if (sort != null)
            controls.Query["orderby"] = sort;
        else if (model.OrderBy.HasValue)
            controls.Query["orderby"] = model.OrderBy.Value.ToString(CultureInfo.InvariantCulture);
        if (model.PageSize > 0)
            controls.Query["pagesize"] = model.PageSize.ToString(CultureInfo.InvariantCulture);
        controls.Query["viewmode"] = model.ViewMode;
        if (search != null)
        {
            controls.Categories = Choices(search.AvailableCategories);
            if (allProducts)
                controls.Categories.Insert(0, new("0", "همهٔ دسته‌ها", search.cid == 0));
            controls.Query["cid"] = search.cid.ToString(CultureInfo.InvariantCulture);
            controls.Query["isc"] = search.isc.ToString().ToLowerInvariant();
        }
        if (controls.NativeSearch)
        {
            controls.Manufacturers = Choices(search!.AvailableManufacturers);
            controls.Query.Remove("ms");
            controls.Query.Remove("specs");
        }
        else
        {
            controls.Manufacturers = model.ManufacturerFilter.Enabled ? Choices(model.ManufacturerFilter.Manufacturers) : [];
            controls.Query["ms"] = string.Join(',', controls.Manufacturers.Where(option => option.Selected).Select(option => option.Value));
            if (model.SpecificationFilter.Enabled)
            {
                controls.Specifications = model.SpecificationFilter.Attributes.Select(attribute => new CatalogFilterGroup(attribute.Id, attribute.Name,
                    attribute.Values.Select(option => new CatalogChoice(option.Id.ToString(CultureInfo.InvariantCulture), option.Name, option.Selected,
                        Regex.IsMatch(option.ColorSquaresRgb ?? "", "^#[0-9a-fA-F]{6}$") ? option.ColorSquaresRgb ?? "" : "")).ToList())).ToList();
            }
            controls.Query["specs"] = string.Join(',', controls.Specifications.SelectMany(group => group.Options).Where(option => option.Selected).Select(option => option.Value));
        }
        if (!controls.PriceEnabled)
            controls.Query.Remove("price");
        ViewData["Placement"] = placement;
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/CatalogControls.cshtml", controls);
    }

    private static List<CatalogChoice> Choices(IEnumerable<SelectListItem> options)
    {
        return options.Select(option => new CatalogChoice(option.Value, option.Text, option.Selected)).ToList();
    }

    private static string Price(decimal? value)
    {
        return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
    }
}