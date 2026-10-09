using Nop.Core.Domain.Menus;
using Nop.Web.Factories;
using Nop.Web.Models.Menus;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class StorefrontNavigation(IMenuModelFactory menus,
    ICommonModelFactory common)
{
    public async Task<StoreNavigation> PrepareAsync(Nop.Web.Models.Common.HeaderLinksModel? header = null)
    {
        var main = (await menus.PrepareMenuModelsAsync(MenuType.Main)).FirstOrDefault();
        var footer = await menus.PrepareMenuModelsAsync(MenuType.Footer);
        header ??= await common.PrepareHeaderLinksModelAsync();
        return new StoreNavigation
        {
            Main = main == null ? new() : From(main),
            Footer = footer.Select(From).ToList(),
            SignedIn = header.IsAuthenticated,
            CartEnabled = header.ShoppingCartEnabled,
            CartCount = header.ShoppingCartItems
        };
    }

    private static StoreMenu From(MenuModel model)
    {
        return new StoreMenu { Id = model.Id, Name = model.Name, Items = model.Items.Select(From).ToList() };
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