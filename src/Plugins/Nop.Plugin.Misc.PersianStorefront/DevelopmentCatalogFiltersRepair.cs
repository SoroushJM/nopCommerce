using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Localization;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;

namespace Nop.Plugin.Misc.PersianStorefront;

// Fixture-only, called by the existing guarded administrator repair operation.
public sealed class DevelopmentCatalogFiltersRepair(IProductService products, ICategoryService categories,
    IProductAttributeService variants, ISpecificationAttributeService specifications,
    IGenericAttributeService attributes, IStoreContext stores, ISettingService settings,
    ILanguageService languages, ILocalizationService localization)
{
    public async Task RunAsync(IEnumerable<Category> fixtureCategories)
    {
        foreach (var category in fixtureCategories)
        {
            if (await attributes.GetAttributeAsync<bool>(category, "Madadrang.PriceFilterPrepared"))
                continue;
            category.PriceRangeFiltering = true;
            await categories.UpdateCategoryAsync(category);
            await attributes.SaveAttributeAsync(category, "Madadrang.PriceFilterPrepared", true);
        }

        var store = await stores.GetCurrentStoreAsync();
        if (!await attributes.GetAttributeAsync<bool>(store, "Madadrang.SearchPriceFilterPrepared"))
        {
            var catalog = await settings.LoadSettingAsync<CatalogSettings>(store.Id);
            if (catalog.SearchPageManuallyPriceRange && catalog.SearchPagePriceFrom == NopCatalogDefaults.DefaultPriceRangeFrom
                && catalog.SearchPagePriceTo == NopCatalogDefaults.DefaultPriceRangeTo
                && !await settings.SettingExistsAsync(catalog, item => item.SearchPageManuallyPriceRange, store.Id)
                && !await settings.SettingExistsAsync(catalog, item => item.SearchPagePriceFrom, store.Id)
                && !await settings.SettingExistsAsync(catalog, item => item.SearchPagePriceTo, store.Id))
            {
                catalog.SearchPageManuallyPriceRange = false;
                await settings.SaveSettingAsync(catalog, item => item.SearchPageManuallyPriceRange, store.Id);
            }
            await attributes.SaveAttributeAsync(store, "Madadrang.SearchPriceFilterPrepared", true);
        }
        await PrepareMissingResourcesAsync();
        var id = await attributes.GetAttributeAsync<int>(store, "Madadrang.ColorSpecificationId");
        var attribute = await specifications.GetSpecificationAttributeByIdAsync(id);
        if (attribute == null)
        {
            attribute = new SpecificationAttribute { Name = "رنگ", DisplayOrder = 0 };
            await specifications.InsertSpecificationAttributeAsync(attribute);
            await attributes.SaveAttributeAsync(store, "Madadrang.ColorSpecificationId", attribute.Id);
        }
        var options = (await specifications.GetSpecificationAttributeOptionsBySpecificationAttributeAsync(attribute.Id)).ToList();
        foreach (var sample in Storefront.UI.SampleCatalog.Products)
        {
            var product = await products.GetProductBySkuAsync("MRG-" + sample.Id);
            if (product == null || product.Deleted || await attributes.GetAttributeAsync<bool>(product, "Madadrang.ColorFilterPrepared"))
                continue;
            var mapped = (await specifications.GetProductSpecificationAttributesAsync(product.Id)).Select(item => item.SpecificationAttributeOptionId).ToHashSet();
            foreach (var mapping in await variants.GetProductAttributeMappingsByProductIdAsync(product.Id))
            {
                if (mapping.AttributeControlType != AttributeControlType.ColorSquares)
                    continue;
                foreach (var value in await variants.GetProductAttributeValuesAsync(mapping.Id))
                {
                    var option = options.FirstOrDefault(item => item.ColorSquaresRgb == value.ColorSquaresRgb && item.Name == value.Name);
                    if (option == null)
                    {
                        option = new SpecificationAttributeOption
                        {
                            SpecificationAttributeId = attribute.Id,
                            Name = value.Name,
                            ColorSquaresRgb = value.ColorSquaresRgb,
                            DisplayOrder = value.DisplayOrder
                        };
                        await specifications.InsertSpecificationAttributeOptionAsync(option);
                        options.Add(option);
                    }
                    if (mapped.Add(option.Id))
                        await specifications.InsertProductSpecificationAttributeAsync(new ProductSpecificationAttribute
                        {
                            ProductId = product.Id,
                            SpecificationAttributeOptionId = option.Id,
                            AttributeType = SpecificationAttributeType.Option,
                            AllowFiltering = true,
                            ShowOnProductPage = false
                        });
                }
            }
            await attributes.SaveAttributeAsync(product, "Madadrang.ColorFilterPrepared", true);
        }
    }

    private async Task PrepareMissingResourcesAsync()
    {
        var resources = new Dictionary<string, string>
        {
            ["Catalog.ViewMode.Grid"] = "نمای شبکه‌ای",
            ["Catalog.ViewMode.List"] = "نمای فهرستی",
            ["Catalog.Products.NoResult"] = "محصولی پیدا نشد",
            ["Search.SearchTermMinimumLengthIsNCharacters"] = "برای جست‌وجو دست‌کم {0} حرف بنویس.",
            ["PageTitle.Search"] = "جست‌وجو",
            ["Products.FeaturedProducts"] = "انتخاب‌های ویژه",
            ["Categories.Breadcrumb.Top"] = "خانه"
        };
        foreach (var language in (await languages.GetAllLanguagesAsync(showHidden: true)).Where(item => item.LanguageCulture.StartsWith("fa", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var resource in resources)
            {
                if (await localization.GetLocaleStringResourceByNameAsync(resource.Key, language.Id, false) != null)
                    continue;
                await localization.InsertLocaleStringResourceAsync(new LocaleStringResource
                {
                    LanguageId = language.Id,
                    ResourceName = resource.Key,
                    ResourceValue = resource.Value
                });
            }
        }
    }
}