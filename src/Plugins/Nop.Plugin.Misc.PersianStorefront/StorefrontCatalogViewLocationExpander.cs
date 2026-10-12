using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.Misc.PersianStorefront;

/// <summary>Retains native/themed catalog partial lookup for the all-products controller.</summary>
public sealed class StorefrontCatalogViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context) { }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (!string.Equals(context.ControllerName, "MadadrangCatalog", StringComparison.OrdinalIgnoreCase))
            return viewLocations;

        // Earlier theme expanders retain their order. Add the corresponding Catalog
        // location beside each controller location, including nested native partials.
        return viewLocations.SelectMany(location => location.Contains("/{1}/", StringComparison.Ordinal)
            ? new[] { location, location.Replace("/{1}/", "/Catalog/", StringComparison.Ordinal) }
            : new[] { location });
    }
}
