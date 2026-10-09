using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Stores;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront.Components;

public sealed class MadadrangNewProductsViewComponent(CatalogSettings settings, IStoreContext stores,
    IProductService products, IProductModelFactory factory) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!settings.NewProductsEnabled)
            return Content("");
        var items = await products.GetProductsMarkedAsNewAsync((await stores.GetCurrentStoreAsync()).Id, pageIndex: 0, pageSize: 4);
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/NewProducts.cshtml",
            (await factory.PrepareProductOverviewModelsAsync(items)).ToList());
    }
}

public sealed class MadadrangCategoryTileViewComponent(ICategoryService categories, IGenericAttributeService attributes) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(CategoryModel model)
    {
        var category = await categories.GetCategoryByIdAsync(model.Id);
        var style = await attributes.GetAttributeAsync<string>(category, "Madadrang.TileStyle");
        ViewData["Style"] = int.TryParse(style, out var index) && index is >= 0 and <= 3 ? index : 0;
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/CategoryTile.cshtml", model);
    }
}

public sealed class MadadrangHomepageBannersViewComponent(IStoreContext stores, IGenericAttributeService attributes,
    ICategoryService categories, IUrlRecordService urls, IAclService acl, IStoreMappingService mappings, INopUrlHelper nopUrl) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await stores.GetCurrentStoreAsync();
        var model = new HomepageBanners();
        model.ArtUrl = await CategoryUrl(await attributes.GetAttributeAsync<int>(store, "Madadrang.ArtCategoryId"));
        model.NotebookUrl = await CategoryUrl(await attributes.GetAttributeAsync<int>(store, "Madadrang.NotebookCategoryId"));
        return await ViewAsync("~/Plugins/Misc.PersianStorefront/Views/HomepageBanners.cshtml", model);
    }

    private async Task<string> CategoryUrl(int id)
    {
        var category = await categories.GetCategoryByIdAsync(id);
        if (category == null || category.Deleted || !category.Published || !await acl.AuthorizeAsync(category) || !await mappings.AuthorizeAsync(category))
            return "";
        return await nopUrl.RouteGenericUrlAsync<Category>(new
        {
            SeName = await urls.GetSeNameAsync(category)
        });
    }
}

public sealed class HomepageBanners
{
    public string ArtUrl { get; set; } = "";
    public string NotebookUrl { get; set; } = "";
}