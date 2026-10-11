using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;
using Nop.Core;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Nop.Core.Domain.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangProductViewComponent(IProductService products, INopUrlHelper nopUrl,
    ILocalizationService localization, IWorkContext workContext, IProductAttributeService attributes,
    IProductAttributeParser parser) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(ProductDetailsModel model)
    {
        // The calling theme chooses its layout after this component has rendered.
        HttpContext.Items["Madadrang.NativePage"] = true;
        var product = await products.GetProductByIdAsync(model.Id);
        ViewData["NativeProduct"] = await NativeProductPresentation.FromAsync(model, Url, nopUrl, localization,
            workContext, product?.OrderMinimumQuantity ?? 1, await InitialDisabledAttributesAsync(model, product));
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NativeProduct.cshtml", model);
    }

    private async Task<List<int>> InitialDisabledAttributesAsync(ProductDetailsModel model, Product? product)
    {
        if (product == null || !model.ProductAttributes.Any(attribute => attribute.HasCondition))
            return [];

        var fields = new Dictionary<string, StringValues>();
        foreach (var attribute in model.ProductAttributes)
        {
            var selected = attribute.Values.FirstOrDefault(value => value.IsPreSelected);
            if (selected != null)
                fields[NopCatalogDefaults.ProductAttributePrefix + attribute.Id] = selected.Id.ToString(CultureInfo.InvariantCulture);
        }
        var selectedXml = await parser.ParseProductAttributesAsync(product, new FormCollection(fields), []);
        var disabled = new List<int>();
        foreach (var mapping in await attributes.GetProductAttributeMappingsByProductIdAsync(product.Id))
        {
            if (await parser.IsConditionMetAsync(mapping, selectedXml) == false)
                disabled.Add(mapping.Id);
        }
        return disabled;
    }
}