using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Seo;
using Nop.Core.Infrastructure;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Media;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed record MishaShopSeedAvailability(bool CanSeed, string Message, int ExistingProducts = 0);

public sealed record MishaShopSeedResult(int AddedProducts, int SkippedProducts, int AddedCategories,
    int AddedManufacturers, int AddedCombinations, IReadOnlyList<string> Messages);

// Only an explicit local administrator operation or opted-in Development installation calls this service.
public sealed class MishaShopDevelopmentCatalogSeeder(IProductService products, ICategoryService categories,
    IManufacturerService manufacturers, IProductAttributeService variants, IProductAttributeParser parser,
    ISpecificationAttributeService specifications, IPictureService pictures, IUrlRecordService urls,
    IGenericAttributeService markers, ICurrencyService currencies, ISettingService settings,
    IStoreContext stores, IWebHostEnvironment environment, IConfiguration configuration)
{
    private const string OwnershipKey = "MishaShop.DevelopmentSeed.V1";
    private static readonly SemaphoreSlim SeedGate = new(1, 1);

    public async Task<MishaShopSeedAvailability> GetAvailabilityAsync()
    {
        if (!environment.IsDevelopment())
            return new(false, "ساخت نمونه‌ها فقط در محیط Development در دسترس است.");
        if (StorefrontThemeService.NormalizeThemeName(configuration["StorefrontThemeConfig:Theme"]) != "MishaShop")
            return new(false, "انتخاب تم این فرایند باید StorefrontThemeConfig__Theme=MishaShop باشد؛ انتخاب تم مشتری کافی نیست.");

        var stationery = await products.SearchProductsAsync(keywords: "MRG-", searchManufacturerPartNumber: false,
            searchSku: true, showHidden: true);
        if (stationery.Any(product => product.Sku?.StartsWith("MRG-", StringComparison.OrdinalIgnoreCase) == true))
            return new(false, "کاتالوگ MRG مداد رنگی در این پایگاه‌داده وجود دارد. اجرای محلی با تغییر تم، دیتابیس را عوض نمی‌کند؛ برای میشا شاپ یک پایگاه‌دادهٔ توسعهٔ مستقل انتخاب کنید.");

        var store = await stores.GetCurrentStoreAsync();
        var currencySettings = await settings.LoadSettingAsync<CurrencySettings>(store.Id);
        var currency = await currencies.GetCurrencyByIdAsync(currencySettings.PrimaryStoreCurrencyId);
        if (!string.Equals(currency?.CurrencyCode, "IRT", StringComparison.OrdinalIgnoreCase))
            return new(false, "قیمت نمونه‌ها به تومان است. پیش از اجرا، در تنظیمات ارز ادمین، تومان با کد IRT را ارز اصلی فروشگاه تعیین کنید. این عملیات ارز یا نرخ تبدیل را تغییر نمی‌دهد.");

        var existing = 0;
        foreach (var sample in MishaShopSampleCatalog.Products)
            if (await products.GetProductBySkuAsync(sample.Sku) is not null)
                existing++;
        return new(true, "داده‌ها به‌صورت افزایشی ساخته می‌شوند؛ هر SKU موجود کاملاً حفظ و از اجرای دوباره کنار گذاشته می‌شود.", existing);
    }

    public async Task<MishaShopSeedResult> SeedAsync()
    {
        await SeedGate.WaitAsync();
        try
        {
            var availability = await GetAvailabilityAsync();
            if (!availability.CanSeed)
                return new(0, 0, 0, 0, 0, [availability.Message]);

            var pending = new List<MishaShopSeedProduct>();
            foreach (var sample in MishaShopSampleCatalog.Products)
                if (await products.GetProductBySkuAsync(sample.Sku) is null)
                    pending.Add(sample);
            var skipped = MishaShopSampleCatalog.Products.Count - pending.Count;
            if (pending.Count == 0)
                return new(0, skipped, 0, 0, 0, ["همهٔ SKUهای نمونه موجودند؛ هیچ رکوردی تغییر نکرد."]);

            // Check every embedded image before the first insert; an incomplete package must not create partial products.
            var imageBytes = await MishaShopSampleCatalog.ReadImagesAsync();
            var imageCache = new Dictionary<string, Picture>(StringComparer.Ordinal);
            var categoryCache = new Dictionary<string, Category>(StringComparer.Ordinal);
            var existingCategories = await categories.GetAllCategoriesAsync(showHidden: true);
            var existingManufacturers = await manufacturers.GetAllManufacturersAsync(showHidden: true);
            var now = DateTime.UtcNow;
            var addedCategories = 0;
            var addedManufacturers = 0;
            var addedCombinations = 0;

            async Task<Picture> PictureForAsync(MishaShopSeedProduct sample)
            {
                if (imageCache.TryGetValue(sample.Image, out var cached)) return cached;
                var picture = await pictures.InsertPictureAsync(imageBytes[sample.Image], "image/svg+xml", sample.Slug,
                    sample.Name + " — تصویرسازی نمونهٔ توسعه", sample.Name);
                imageCache.Add(sample.Image, picture);
                return picture;
            }

            Manufacturer? manufacturer = null;
            foreach (var candidate in existingManufacturers)
                if (await markers.GetAttributeAsync<string>(candidate, OwnershipKey) == "manufacturer")
                {
                    manufacturer = candidate;
                    break;
                }
            if (manufacturer is null)
            {
                manufacturer = new Manufacturer
                {
                    Name = "میشا شاپ", ManufacturerTemplateId = 1, Published = true, PageSize = 6,
                    AllowCustomersToSelectPageSize = true, PageSizeOptions = "6,12,24", PriceRangeFiltering = true,
                    ManuallyPriceRange = false, CreatedOnUtc = now, UpdatedOnUtc = now
                };
                await manufacturers.InsertManufacturerAsync(manufacturer);
                await markers.SaveAttributeAsync(manufacturer, OwnershipKey, "manufacturer");
                await SaveSlugAsync(manufacturer, "mishashop-dev-collection", manufacturer.Name);
                addedManufacturers++;
            }

            var colorAttribute = await ProductAttributeAsync("رنگ");
            var sizeAttribute = await ProductAttributeAsync("سایز");
            var colorSpecification = await SpecificationAsync("color", "رنگ");
            var sizeSpecification = await SpecificationAsync("size", "سایز");
            var colorOptions = (await specifications.GetSpecificationAttributeOptionsBySpecificationAttributeAsync(colorSpecification.Id)).ToList();
            var sizeOptions = (await specifications.GetSpecificationAttributeOptionsBySpecificationAttributeAsync(sizeSpecification.Id)).ToList();

            foreach (var sample in pending)
            {
                if (!categoryCache.TryGetValue(sample.CategoryKey, out var category))
                {
                    category = null;
                    foreach (var candidate in existingCategories)
                        if (await markers.GetAttributeAsync<string>(candidate, OwnershipKey) == "category:" + sample.CategoryKey)
                        {
                            category = candidate;
                            break;
                        }
                    if (category is null)
                    {
                        category = new Category
                        {
                            Name = sample.Category, CategoryTemplateId = 1, Published = true, ShowOnHomepage = true,
                            PageSize = 6, AllowCustomersToSelectPageSize = true, PageSizeOptions = "6,12,24",
                            PriceRangeFiltering = true, ManuallyPriceRange = false, DisplayOrder = categoryCache.Count,
                            PictureId = (await PictureForAsync(sample)).Id, CreatedOnUtc = now, UpdatedOnUtc = now
                        };
                        await categories.InsertCategoryAsync(category);
                        await markers.SaveAttributeAsync(category, OwnershipKey, "category:" + sample.CategoryKey);
                        await SaveSlugAsync(category, "mishashop-dev-" + sample.CategoryKey, category.Name);
                        addedCategories++;
                    }
                    categoryCache.Add(sample.CategoryKey, category);
                }

                var outOfStock = sample.Id is 1 or 8 ? 1 : 0;
                var product = new Product
                {
                    Name = sample.Name, ShortDescription = sample.Description, FullDescription = "<p>" + sample.Description + "</p>",
                    Sku = sample.Sku, AdminComment = "MishaShop development sample v1; local illustration, not merchant stock.",
                    ProductType = ProductType.SimpleProduct, ProductTemplateId = 1, Published = false, VisibleIndividually = true,
                    Price = sample.PriceInToman, OldPrice = sample.OldPriceInToman,
                    ShowOnHomepage = sample.Id <= 6, MarkAsNew = sample.Id > 6, DisplayOrder = sample.Id,
                    OrderMinimumQuantity = 1, OrderMaximumQuantity = 8, IsShipEnabled = true, Weight = .4m,
                    ManageInventoryMethod = ManageInventoryMethod.ManageStockByAttributes,
                    AllowAddingOnlyExistingAttributeCombinations = true, BackorderMode = BackorderMode.NoBackorders,
                    StockQuantity = (sample.Colors.Count * sample.Sizes.Count - outOfStock) * 8,
                    DisplayStockAvailability = true, CreatedOnUtc = now, UpdatedOnUtc = now
                };
                await products.InsertProductAsync(product);
                await SaveSlugAsync(product, sample.Slug, product.Name);
                await categories.InsertProductCategoryAsync(new ProductCategory { ProductId = product.Id, CategoryId = category.Id, DisplayOrder = sample.Id });
                await manufacturers.InsertProductManufacturerAsync(new ProductManufacturer { ProductId = product.Id, ManufacturerId = manufacturer.Id });
                await products.InsertProductPictureAsync(new ProductPicture { ProductId = product.Id, PictureId = (await PictureForAsync(sample)).Id });

                var colorMapping = new ProductAttributeMapping { ProductId = product.Id, ProductAttributeId = colorAttribute.Id,
                    IsRequired = true, TextPrompt = "رنگ را انتخاب کنید", AttributeControlType = AttributeControlType.ColorSquares };
                var sizeMapping = new ProductAttributeMapping { ProductId = product.Id, ProductAttributeId = sizeAttribute.Id,
                    IsRequired = true, TextPrompt = "سایز را انتخاب کنید", AttributeControlType = AttributeControlType.RadioList, DisplayOrder = 1 };
                await variants.InsertProductAttributeMappingAsync(colorMapping);
                await variants.InsertProductAttributeMappingAsync(sizeMapping);
                var colors = new List<ProductAttributeValue>();
                var sizes = new List<ProductAttributeValue>();
                foreach (var color in sample.Colors)
                {
                    var value = new ProductAttributeValue { ProductAttributeMappingId = colorMapping.Id, Name = color.Name,
                        ColorSquaresRgb = color.Hex, AttributeValueType = AttributeValueType.Simple, Quantity = 1,
                        IsPreSelected = colors.Count == 0, DisplayOrder = colors.Count };
                    await variants.InsertProductAttributeValueAsync(value);
                    colors.Add(value);
                    await AddSpecificationAsync(product.Id, colorSpecification.Id, colorOptions, color.Name, color.Hex);
                }
                foreach (var size in sample.Sizes)
                {
                    var value = new ProductAttributeValue { ProductAttributeMappingId = sizeMapping.Id, Name = size,
                        AttributeValueType = AttributeValueType.Simple, Quantity = 1, IsPreSelected = sizes.Count == 0, DisplayOrder = sizes.Count };
                    await variants.InsertProductAttributeValueAsync(value);
                    sizes.Add(value);
                    await AddSpecificationAsync(product.Id, sizeSpecification.Id, sizeOptions, size, "");
                }
                for (var color = 0; color < colors.Count; color++)
                    for (var size = 0; size < sizes.Count; size++)
                    {
                        var xml = parser.AddProductAttribute("", colorMapping, colors[color].Id.ToString());
                        xml = parser.AddProductAttribute(xml, sizeMapping, sizes[size].Id.ToString());
                        var unavailable = (sample.Id is 1 or 8) && color == colors.Count - 1 && size == sizes.Count - 1;
                        await variants.InsertProductAttributeCombinationAsync(new ProductAttributeCombination
                        {
                            ProductId = product.Id, AttributesXml = xml, StockQuantity = unavailable ? 0 : 8,
                            AllowOutOfStockOrders = false, MinStockQuantity = 1, Sku = $"{sample.Sku}-C{color + 1}-S{size + 1}"
                        });
                        addedCombinations++;
                    }
                // Only this newly-created product is updated; existing SKU records are never touched.
                product.Published = true;
                await products.UpdateProductAsync(product);
            }

            return new(pending.Count, skipped, addedCategories, addedManufacturers, addedCombinations,
                ["محصولات تازه همراه تصویر محلی، رنگ/سایز، موجودی ترکیب‌ها، slug بومی و فیلتر مشخصات ساخته شدند.",
                 "هیچ محصول موجود، منو، تصویر hero، تنظیم ارز یا تنظیم پرداخت بازنویسی نشد."]);
        }
        finally { SeedGate.Release(); }
    }

    private async Task<ProductAttribute> ProductAttributeAsync(string name)
    {
        var attribute = (await variants.GetAllProductAttributesAsync()).FirstOrDefault(item => item.Name == name);
        if (attribute is not null) return attribute;
        attribute = new ProductAttribute { Name = name };
        await variants.InsertProductAttributeAsync(attribute);
        return attribute;
    }

    private async Task<SpecificationAttribute> SpecificationAsync(string key, string name)
    {
        foreach (var candidate in await specifications.GetAllSpecificationAttributesAsync())
            if (await markers.GetAttributeAsync<string>(candidate, OwnershipKey) == "specification:" + key)
                return candidate;
        var attribute = new SpecificationAttribute { Name = name, DisplayOrder = key == "color" ? 0 : 1 };
        await specifications.InsertSpecificationAttributeAsync(attribute);
        await markers.SaveAttributeAsync(attribute, OwnershipKey, "specification:" + key);
        return attribute;
    }

    private async Task AddSpecificationAsync(int productId, int attributeId, List<SpecificationAttributeOption> options, string name, string color)
    {
        var option = options.FirstOrDefault(item => item.Name == name && (item.ColorSquaresRgb ?? "") == color);
        if (option is null)
        {
            option = new SpecificationAttributeOption { SpecificationAttributeId = attributeId, Name = name, ColorSquaresRgb = color, DisplayOrder = options.Count };
            await specifications.InsertSpecificationAttributeOptionAsync(option);
            options.Add(option);
        }
        await specifications.InsertProductSpecificationAttributeAsync(new ProductSpecificationAttribute
        {
            ProductId = productId, SpecificationAttributeOptionId = option.Id, AttributeType = SpecificationAttributeType.Option,
            AllowFiltering = true, ShowOnProductPage = true
        });
    }

    private async Task SaveSlugAsync<T>(T entity, string slug, string name) where T : BaseEntity, ISlugSupported =>
        await urls.SaveSlugAsync(entity, await urls.ValidateSeNameAsync(entity, slug, name, true), 0);
}

public sealed class MishaShopDevelopmentSeedStartup : INopStartup
{
    public int Order => 251;
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<MishaShopDevelopmentCatalogSeeder>();
    public void Configure(IApplicationBuilder application) { }
}
