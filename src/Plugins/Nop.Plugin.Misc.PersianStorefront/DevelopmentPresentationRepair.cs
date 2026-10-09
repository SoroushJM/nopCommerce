using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Menus;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Topics;
using Nop.Core.Http;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Menus;
using Nop.Services.Seo;
using Nop.Services.Topics;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Misc.PersianStorefront;

// Called only by the guarded, explicit local administrator operation.
public sealed class DevelopmentPresentationRepair(IProductService products, ICategoryService categories,
    IManufacturerService manufacturers, IUrlRecordService urls, IGenericAttributeService attributes,
    IStoreContext stores, IMenuService menus, ITopicService topics, ILocalizedEntityService localized, ITopicTemplateService topicTemplates)
{
    public async Task<List<string>> RunAsync()
    {
        var report = new List<string>();
        var fixtureCategories = new Dictionary<string, Category>();
        foreach (var sample in Storefront.UI.SampleCatalog.Products)
        {
            var product = await products.GetProductBySkuAsync("MRG-" + sample.Id);
            if (product == null || product.Deleted)
                continue;
            await EnsureSlugAsync(product, product.Name);
            if (sample.Id <= 4 && !product.ShowOnHomepage)
            {
                product.ShowOnHomepage = true;
                product.DisplayOrder = sample.Id;
                await products.UpdateProductAsync(product);
            }
            if (sample.Id > 4 && !product.MarkAsNew)
            {
                product.MarkAsNew = true;
                await products.UpdateProductAsync(product);
            }
            var associations = await categories.GetProductCategoriesByProductIdAsync(product.Id, showHidden: true);
            var association = associations.Count == 1 ? associations[0] : null;
            if (association != null)
            {
                var category = await categories.GetCategoryByIdAsync(association.CategoryId);
                if (category != null && !category.Deleted)
                {
                    fixtureCategories.TryAdd(sample.Category, category);
                    await EnsureSlugAsync(category, category.Name);
                }
            }
            foreach (var mapping in await manufacturers.GetProductManufacturersByProductIdAsync(product.Id, showHidden: true))
            {
                var manufacturer = await manufacturers.GetManufacturerByIdAsync(mapping.ManufacturerId);
                if (manufacturer != null && !manufacturer.Deleted)
                    await EnsureSlugAsync(manufacturer, manufacturer.Name);
            }
        }

        string[] initialOrder = ["دفتر و کاغذ", "نوشت‌افزار", "لوازم مدرسه", "هنر و خلاقیت"];
        if (fixtureCategories.Count == 0)
            return ["دسته‌های مرتبط با داده‌های نمونه پیدا نشدند؛ منوها تغییر نکردند."];
        for (var index = 0; index < initialOrder.Length; index++)
        {
            if (!fixtureCategories.TryGetValue(initialOrder[index], out var category))
                continue;
            if (!category.ShowOnHomepage)
            {
                category.ShowOnHomepage = true;
                category.DisplayOrder = index;
                await categories.UpdateCategoryAsync(category);
            }
            if (await attributes.GetAttributeAsync<string>(category, "Madadrang.TileStyle") == null)
                await attributes.SaveAttributeAsync(category, "Madadrang.TileStyle", index.ToString());
        }
        var store = await stores.GetCurrentStoreAsync();
        foreach (var banner in new[] { (Key: "Madadrang.ArtCategoryId", Name: "هنر و خلاقیت"), (Key: "Madadrang.NotebookCategoryId", Name: "دفتر و کاغذ") })
            if (fixtureCategories.TryGetValue(banner.Name, out var category) && await attributes.GetAttributeAsync<int>(store, banner.Key) == 0)
                await attributes.SaveAttributeAsync(store, banner.Key, category.Id);

        // Retain edits to owned menus; only insert records on the first explicit setup.
        await CreateMenuAsync("madadrang-main", "فروشگاه", MenuType.Main,
            [Link("همهٔ محصولات", "/stationery/catalog", "sf-menu-all"),
             .. initialOrder.Where(fixtureCategories.ContainsKey).Select(name => new MenuItem
             { MenuItemType = MenuItemType.Category, EntityId = fixtureCategories[name].Id }),
             Link("پیشنهادهای رنگی", "/stationery/catalog?orderby=10", "sf-menu-offer")]);
        await CreateMenuAsync("madadrang-footer-shop", "دنیای مدادرنگ", MenuType.Footer,
            [Link("همهٔ محصولات", "/stationery/catalog"),
             .. initialOrder.Where(fixtureCategories.ContainsKey).Select(name => new MenuItem
             { MenuItemType = MenuItemType.Category, EntityId = fixtureCategories[name].Id })]);
        await CreateMenuAsync("madadrang-footer-help", "کنار تو هستیم", MenuType.Footer,
            [Link("سبد خرید", "/stationery/cart"), Link("حساب من", "/stationery/login"),
             new MenuItem { MenuItemType = MenuItemType.StandardPage, RouteName = NopRouteNames.General.CONTACT_US, Title = "تماس با ما" }]);
        await DisableUntouchedDefaultsAsync(report);
        await HideUntouchedHomepageTextAsync();
        report.Insert(0, $"نمایش نمونه‌ها آماده شد؛ {fixtureCategories.Count} دستهٔ مرتبط شناسایی شد. قیمت و موجودی تغییر نکردند.");
        return report;
    }

    private async Task EnsureSlugAsync<T>(T entity, string name) where T : BaseEntity, ISlugSupported
    {
        if (string.IsNullOrWhiteSpace(await urls.GetActiveSlugAsync(entity.Id, typeof(T).Name, 0)))
            await urls.SaveSlugAsync(entity, await urls.ValidateSeNameAsync(entity, "", name, true), 0);
    }

    private static MenuItem Link(string title, string url, string css = "")
    {
        return new()
        {
            Title = title,
            Url = url,
            CssClass = css,
            MenuItemType = MenuItemType.CustomLink
        };
    }

    private async Task CreateMenuAsync(string marker, string name, MenuType type, List<MenuItem> items)
    {
        if ((await menus.GetAllMenusAsync(showHidden: true)).Any(menu => menu.CssClass == marker))
            return;
        var menu = new Menu { Name = name, MenuType = type, CssClass = marker, Published = true, DisplayOrder = -10 };
        await menus.InsertMenuAsync(menu);
        for (var index = 0; index < items.Count; index++)
        {
            items[index].MenuId = menu.Id;
            items[index].Published = true;
            items[index].DisplayOrder = index;
            await menus.InsertMenuItemAsync(items[index]);
        }
    }

    private async Task DisableUntouchedDefaultsAsync(List<string> report)
    {
        var definitions = new Dictionary<string, (MenuType Type, int Order, string[] Items)>
        {
            ["Menu"] = (MenuType.Main, 0, [Standard("Home page", NopRouteNames.General.HOMEPAGE), Standard("New products", NopRouteNames.General.NEW_PRODUCTS), Standard("Search", NopRouteNames.General.SEARCH), Standard("My account", NopRouteNames.General.CUSTOMER_INFO), Standard("Blog", NopRouteNames.General.BLOG), Standard("Contact us", NopRouteNames.General.CONTACT_US)]),
            ["Customer service"] = (MenuType.Footer, 1, [Standard("Search", NopRouteNames.General.SEARCH), Standard("Blog", NopRouteNames.General.BLOG), Standard("Recently viewed products", NopRouteNames.General.RECENTLY_VIEWED_PRODUCTS), Standard("Compare products list", NopRouteNames.General.COMPARE_PRODUCTS), Standard("New products", NopRouteNames.General.NEW_PRODUCTS)]),
            ["My account"] = (MenuType.Footer, 2, [Standard("My account", NopRouteNames.General.CUSTOMER_INFO), Standard("Orders", NopRouteNames.General.CUSTOMER_ORDERS), Standard("Withdraw contract", NopRouteNames.General.WITHDRAWAL_REQUEST_FORM, false), Standard("Addresses", NopRouteNames.General.CUSTOMER_ADDRESSES), Standard("Shopping cart", NopRouteNames.General.CART), Standard("Wishlist", NopRouteNames.General.WISHLIST), Standard("Apply for vendor account", NopRouteNames.General.APPLY_VENDOR_ACCOUNT)])
        };
        var information = new List<string> { Standard("Sitemap", NopRouteNames.General.SITEMAP), Standard("Contact us", NopRouteNames.General.CONTACT_US) };
        foreach (var name in new[] { "ShippingInfo", "PrivacyInfo", "ConditionsOfUse", "AboutUs" })
        {
            var topic = await topics.GetTopicBySystemNameAsync(name);
            information.Add(Fingerprint(new MenuItem { MenuItemType = MenuItemType.TopicPage, EntityId = topic?.Id, Published = true }));
        }
        definitions["Information"] = (MenuType.Footer, 0, information.ToArray());
        foreach (var menu in await menus.GetAllMenusAsync(showHidden: true))
        {
            if (!menu.Published || !definitions.TryGetValue(menu.Name, out var definition))
                continue;
            var items = await menus.GetAllMenuItemsAsync(menuId: menu.Id, showHidden: true);
            var translated = (await localized.GetEntityLocalizedPropertiesAsync(menu.Id, nameof(Menu), nameof(Menu.Name))).Any();
            foreach (var item in items)
                translated |= (await localized.GetEntityLocalizedPropertiesAsync(item.Id, nameof(MenuItem), nameof(MenuItem.Title))).Any();
            var untouched = menu.MenuType == definition.Type && menu.DisplayOrder == definition.Order &&
                !translated &&
                !menu.DisplayAllCategories && !menu.SubjectToAcl && !menu.LimitedToStores && string.IsNullOrEmpty(menu.CssClass) &&
                items.All(item => item.ParentId == 0 && item.DisplayOrder == 0 && !item.SubjectToAcl && !item.LimitedToStores &&
                    string.IsNullOrEmpty(item.CssClass) && string.IsNullOrEmpty(item.Url) && item.TemplateId == 0 &&
                    item.MaximumNumberEntities == null && item.NumberOfItemsPerGridRow == null && item.NumberOfSubItemsPerGridElement == null) &&
                items.Select(Fingerprint).Order().SequenceEqual(definition.Items.Order());
            if (untouched)
            {
                menu.Published = false;
                await menus.UpdateMenuAsync(menu);
            }
            else
                report.Add($"منوی ویرایش‌شدهٔ «{menu.Name}» حفظ شد.");
        }
    }

    private static string Standard(string title, string route, bool published = true)
    {
        return Fingerprint(new MenuItem { Title = title, RouteName = route, MenuItemType = MenuItemType.StandardPage, Published = published });
    }

    private static string Fingerprint(MenuItem item)
    {
        return $"{item.MenuItemTypeId}|{item.Title}|{item.RouteName}|{item.EntityId}|{item.Published}";
    }

    private async Task HideUntouchedHomepageTextAsync()
    {
        const string installerBody = "<p>Online shopping is the process consumers go through to purchase products or services over the Internet. You can edit this in the admin site.</p><p>If you have questions, see the <a href=\"http://docs.nopcommerce.com/\">Documentation</a>, or post in the <a href=\"https://www.nopcommerce.com/boards/\">Forums</a> at <a href=\"https://www.nopcommerce.com\">nopCommerce.com</a></p>";
        var topic = await topics.GetTopicBySystemNameAsync("HomepageText");
        if (topic == null || !topic.Published || topic.Title != "Welcome to our store" || topic.Body != installerBody ||
            topic.IsPasswordProtected || topic.SubjectToAcl || topic.LimitedToStores || topic.IncludeInSitemap ||
            topic.AvailableStartDateTimeUtc != null || topic.AvailableEndDateTimeUtc != null || topic.AccessibleWhenStoreClosed ||
            !string.IsNullOrEmpty(topic.Password) || !string.IsNullOrEmpty(topic.MetaTitle) ||
            !string.IsNullOrEmpty(topic.MetaDescription) || !string.IsNullOrEmpty(topic.MetaKeywords) ||
            topic.DisplayOrder != 1 || await attributes.GetAttributeAsync<bool>(topic, "Madadrang.DefaultHomeTopicHandled"))
            return;
        foreach (var key in new[] { nameof(Topic.Title), nameof(Topic.Body), nameof(Topic.MetaTitle), nameof(Topic.MetaDescription), nameof(Topic.MetaKeywords) })
            if ((await localized.GetEntityLocalizedPropertiesAsync(topic.Id, nameof(Topic), key)).Any())
                return;
        var template = await topicTemplates.GetTopicTemplateByIdAsync(topic.TopicTemplateId);
        if (template?.Name != "Default template" || template.ViewPath != "TopicDetails")
            return;
        topic.Published = false;
        await topics.UpdateTopicAsync(topic);
        await attributes.SaveAttributeAsync(topic, "Madadrang.DefaultHomeTopicHandled", true);
    }
}