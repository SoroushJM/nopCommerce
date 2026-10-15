using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework;

namespace Nop.Plugin.Misc.PersianStorefront;

/// <summary>Supplies explicit plugin-owned calendar controls on native admin transfer pages.</summary>
public sealed class AdminCalendarViewLocationExpander : IViewLocationExpander
{
    private const string CacheKey = "PersianStorefront.AdminCalendarViews";
    private static readonly HashSet<string> _pages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Category/List", "Country/List", "Customer/List", "FilterLevelValue/List", "Language/Edit",
        "Manufacturer/List", "NewsLetterSubscription/List", "Order/Edit", "Order/List",
        "Order/ShipmentDetails", "Order/ShipmentList", "PriceList/List", "Product/List",
        "Report/BestCustomersByNumberOfOrders", "Report/BestCustomersByOrderTotal", "Report/Bestsellers",
        "Report/CountrySales", "Report/LowStock", "Report/NeverSold", "Report/RegisteredCustomers",
        "Report/SalesSummary"
    };

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var isNativeTransferPage = string.Equals(context.AreaName, AreaNames.ADMIN, StringComparison.OrdinalIgnoreCase)
            && context.ActionContext.ActionDescriptor is ControllerActionDescriptor action
            && action.ControllerTypeInfo.Assembly == typeof(BaseAdminController).Assembly
            && _pages.Contains($"{context.ControllerName}/{action.ActionName}");
        context.Values[CacheKey] = isNativeTransferPage ? "1" : "0";
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        if (!context.Values.TryGetValue(CacheKey, out var enabled) || enabled != "1")
            return viewLocations;

        return new[]
        {
            "/Plugins/Misc.PersianStorefront/Views/Admin/{1}/{0}.cshtml",
            "/Plugins/Misc.PersianStorefront/Views/Admin/Shared/{0}.cshtml"
        }.Concat(viewLocations);
    }
}
