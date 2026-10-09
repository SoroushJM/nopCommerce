using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework.Components;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangProductCardViewComponent(ICategoryService categories, IManufacturerService manufacturers,
    IProductAttributeService attributes, ILocalizationService localization, IAclService acl, IStoreMappingService stores,
    IProductService products) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(ProductOverviewModel model)
    {
        var caption = new List<string>();
        foreach (var mapping in await manufacturers.GetProductManufacturersByProductIdAsync(model.Id))
        {
            var manufacturer = await manufacturers.GetManufacturerByIdAsync(mapping.ManufacturerId);
            if (manufacturer != null && manufacturer.Published && !manufacturer.Deleted && await acl.AuthorizeAsync(manufacturer) && await stores.AuthorizeAsync(manufacturer))
            {
                caption.Add(await localization.GetLocalizedAsync(manufacturer, item => item.Name));
                break;
            }
        }
        foreach (var mapping in await categories.GetProductCategoriesByProductIdAsync(model.Id))
        {
            var category = await categories.GetCategoryByIdAsync(mapping.CategoryId);
            if (category != null && category.Published && !category.Deleted && await acl.AuthorizeAsync(category) && await stores.AuthorizeAsync(category))
            {
                caption.Add(await localization.GetLocalizedAsync(category, item => item.Name));
                break;
            }
        }
        var colors = new List<string>();
        foreach (var mapping in await attributes.GetProductAttributeMappingsByProductIdAsync(model.Id))
        {
            if (mapping.AttributeControlType != AttributeControlType.ColorSquares)
                continue;
            colors.AddRange((await attributes.GetProductAttributeValuesAsync(mapping.Id))
                .Select(value => value.ColorSquaresRgb)
                .Where(value => value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$")));
        }
        ViewData["Caption"] = string.Join(" · ", caption);
        ViewData["Colors"] = colors.Distinct().Take(3).ToList();
        var product = await products.GetProductByIdAsync(model.Id);
        ViewData["InStock"] = product?.ManageInventoryMethod switch
        {
            ManageInventoryMethod.DontManageStock => true,
            ManageInventoryMethod.ManageStock => product.BackorderMode != BackorderMode.NoBackorders || await products.GetTotalStockQuantityAsync(product) > 0,
            ManageInventoryMethod.ManageStockByAttributes => (await attributes.GetAllProductAttributeCombinationsAsync(product.Id)).Any(combination => combination.StockQuantity > 0 || combination.AllowOutOfStockOrders),
            _ => false
        };
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/ProductCard.cshtml", model);
    }
}