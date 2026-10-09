using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Media;
using Nop.Services.Plugins;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class PersianStorefrontPlugin(DevelopmentCatalogSeeder seeder, IWebHostEnvironment env, IWebHelper web) : BasePlugin
{
    public override string GetConfigurationPageUrl()
    {
        return web.GetStoreLocation() + "Admin/PersianStorefrontMaintenance/Configure";
    }

    public override async Task InstallAsync()
    {
        if (env.IsDevelopment())
            await seeder.Seed();
        await base.InstallAsync();
    }
}
// Runs only on explicit plugin installation in Development. Never replaces merchant data.
public sealed class DevelopmentCatalogSeeder(IProductService products, ICategoryService categories, IManufacturerService manufacturers,
 IProductAttributeService attributes, IProductAttributeParser parser, IPictureService pictures, ICurrencyService currencies,
 ISettingService settings, IWebHostEnvironment environment)
{
    public async Task Seed()
    {
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Development-only sample catalogue.");
        var now = DateTime.UtcNow;
        var categoryMap = new Dictionary<string, int>();
        var manufacturerMap = new Dictionary<string, int>();
        foreach (var name in SampleCatalog.Products.Select(x => x.Category).Distinct())
        {
            var existing = (await categories.GetAllCategoriesAsync(showHidden: true)).FirstOrDefault(x => x.Name == name);
            if (existing == null)
            {
                existing = new Category { Name = name, Published = true, CreatedOnUtc = now, UpdatedOnUtc = now, PageSize = 12, AllowCustomersToSelectPageSize = true, PageSizeOptions = "12,24,36", CategoryTemplateId = 1 };
                await categories.InsertCategoryAsync(existing);
            }
            categoryMap[name] = existing.Id;
        }
        foreach (var name in SampleCatalog.Products.Select(x => x.Brand).Distinct())
        {
            var existing = (await manufacturers.GetAllManufacturersAsync(showHidden: true)).FirstOrDefault(x => x.Name == name);
            if (existing == null)
            {
                existing = new Manufacturer { Name = name, Published = true, CreatedOnUtc = now, UpdatedOnUtc = now, PageSize = 12, ManufacturerTemplateId = 1 };
                await manufacturers.InsertManufacturerAsync(existing);
            }
            manufacturerMap[name] = existing.Id;
        }
        var color = (await attributes.GetAllProductAttributesAsync()).FirstOrDefault(x => x.Name == "رنگ");
        if (color == null)
        {
            color = new Nop.Core.Domain.Catalog.ProductAttribute { Name = "رنگ" };
            await attributes.InsertProductAttributeAsync(color);
        }
        foreach (var sample in SampleCatalog.Products)
        {
            if (await products.GetProductBySkuAsync("MRG-" + sample.Id) != null)
                continue;
            var product = new Product { Name = sample.Name, ShortDescription = sample.Description, FullDescription = sample.Description, Sku = "MRG-" + sample.Id, Published = true, VisibleIndividually = true, ProductType = ProductType.SimpleProduct, ProductTemplateId = 1, Price = sample.Price, OldPrice = sample.OldPrice, OrderMinimumQuantity = 1, OrderMaximumQuantity = 20, IsShipEnabled = true, Weight = .2m, ManageInventoryMethod = ManageInventoryMethod.ManageStockByAttributes, StockQuantity = 30, DisableBuyButton = !sample.Available, CreatedOnUtc = now, UpdatedOnUtc = now };
            await products.InsertProductAsync(product);
            await categories.InsertProductCategoryAsync(new ProductCategory { ProductId = product.Id, CategoryId = categoryMap[sample.Category] });
            await manufacturers.InsertProductManufacturerAsync(new ProductManufacturer { ProductId = product.Id, ManufacturerId = manufacturerMap[sample.Brand] });
            var mapping = new ProductAttributeMapping { ProductId = product.Id, ProductAttributeId = color.Id, IsRequired = true, AttributeControlType = AttributeControlType.ColorSquares };
            await attributes.InsertProductAttributeMappingAsync(mapping);
            foreach (var option in sample.Attributes[0].Options)
            {
                var value = new ProductAttributeValue { ProductAttributeMappingId = mapping.Id, Name = option.Name, ColorSquaresRgb = option.Color, PriceAdjustment = option.Adjustment, AttributeValueType = AttributeValueType.Simple, Quantity = 1, IsPreSelected = option.Id == 1, DisplayOrder = option.Id };
                await attributes.InsertProductAttributeValueAsync(value);
                var xml = parser.AddProductAttribute("", mapping, value.Id.ToString());
                await attributes.InsertProductAttributeCombinationAsync(new ProductAttributeCombination { ProductId = product.Id, AttributesXml = xml, StockQuantity = option.Available && sample.Available ? 10 : 0, AllowOutOfStockOrders = false, Sku = product.Sku + "-" + option.Id });
            }
            var image = Path.Combine(environment.ContentRootPath, "Plugins", "Misc.PersianStorefront", "Content", "Storefront.UI", "images", Path.GetFileName(sample.Image));
            if (File.Exists(image))
            {
                var picture = await pictures.InsertPictureAsync(await File.ReadAllBytesAsync(image), "image/jpeg", product.Sku);
                await products.InsertProductPictureAsync(new ProductPicture { ProductId = product.Id, PictureId = picture.Id });
            }
        }
        var toman = await currencies.GetCurrencyByCodeAsync("IRT");
        if (toman == null)
        {
            toman = new Currency { Name = "تومان", CurrencyCode = "IRT", Rate = 1, Published = true, DisplayLocale = "en-US", CustomFormatting = "#,##0 تومان", CreatedOnUtc = now, UpdatedOnUtc = now };
            await currencies.InsertCurrencyAsync(toman);
        }
        var currencySettings = await settings.LoadSettingAsync<CurrencySettings>();
        currencySettings.PrimaryStoreCurrencyId = toman.Id;
        currencySettings.PrimaryExchangeRateCurrencyId = toman.Id;
        await settings.SaveSettingAsync(currencySettings);
        var orderSettings = await settings.LoadSettingAsync<OrderSettings>();
        orderSettings.AnonymousCheckoutAllowed = false;
        await settings.SaveSettingAsync(orderSettings);
    }
}