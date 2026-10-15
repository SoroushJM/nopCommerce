using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Menus;
using Nop.Core.Domain.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Web.Factories;
using Nop.Core.Http;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Menus;
using Storefront.UI.Shared;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontNavigation(IMenuModelFactory menus,
    ICommonModelFactory common, INopUrlHelper urls, IWorkContext work, ILocalizationService localization,
    IWebHelper web, IHttpContextAccessor http)
{
    public async Task<bool> RequiresNativeMainMenuAsync()
    {
        var main = (await menus.PrepareMenuModelsAsync(MenuType.Main)).FirstOrDefault();
        return main != null && main.Items.Any(RequiresNative);
    }

    private static bool RequiresNative(MenuItemModel item) =>
        item.ChildrenItems.Count > 0 && item.Template == MenuItemTemplate.Grid || item.ChildrenItems.Any(RequiresNative);

    public async Task<StoreNavigation> PrepareAsync(Nop.Web.Models.Common.HeaderLinksModel? header = null)
    {
        var main = (await menus.PrepareMenuModelsAsync(MenuType.Main)).FirstOrDefault();
        var footer = await menus.PrepareMenuModelsAsync(MenuType.Footer);
        header ??= await common.PrepareHeaderLinksModelAsync();
        var labels = await NativeUiLocalization.PrepareAsync(localization, work);
        var request = http.HttpContext?.Request;
        var returnUrl = request == null ? "" : web.GetRawUrl(request);
        if (request != null && request.Query.TryGetValue("returnUrl", out var continuation)
            && !string.IsNullOrEmpty(continuation.ToString()))
            returnUrl = continuation.ToString();
        return new StoreNavigation
        {
            Main = main == null ? new() { Rtl = labels.Rtl, Labels = labels } : From(main, labels),
            Footer = footer.Select(menu => From(menu, labels)).ToList(),
            SignedIn = header.IsAuthenticated,
            CartEnabled = header.ShoppingCartEnabled,
            CartCount = header.ShoppingCartItems,
            CartUrl = urls.RouteUrl(NopRouteNames.General.CART),
            RegistrationEnabled = header.RegistrationType != UserRegistrationType.Disabled,
            RegisterUrl = urls.RouteUrl(NopRouteNames.Standard.REGISTER, new { returnUrl }),
            WishlistEnabled = header.WishlistEnabled,
            WishlistUrl = urls.RouteUrl(NopRouteNames.General.WISHLIST),
            WishlistQuantity = labels.Format("Wishlist.HeaderQuantity", header.WishlistItems)
        };
    }

    private static StoreMenu From(MenuModel model, NativeUiLabels labels)
    {
        return new StoreMenu { Rtl = labels.Rtl, Labels = labels, Id = model.Id, Name = model.Name, Items = model.Items.Select(From).ToList() };
    }

    private static StoreMenuItem From(MenuItemModel model)
    {
        return new StoreMenuItem
        {
            Id = model.Id,
            EntityId = model.EntityId,
            EntityType = (int)model.MenuItemType,
            Title = model.Title ?? "",
            Url = model.Url ?? "",
            CssClass = model.CssClass ?? "",
            Children = model.ChildrenItems.Select(From).ToList()
        };
    }
}
