using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Data;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Factories;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.PersianStorefront;

// Composition fills the all-products gap without replacing native category/search factories.
public sealed class NativeCatalogModelFactory(ICatalogModelFactory catalog, IProductModelFactory overview,
    IProductService products, ICategoryService categories, IManufacturerService manufacturers,
    ISpecificationAttributeService specifications, ILocalizationService localization,
    ICurrencyService currencies, IWorkContext work, IStoreContext store, CatalogSettings settings,
    IAclService acl, IStoreMappingService stores, IRepository<Product> productRepository,
    IRepository<ProductCategory> categoryMappings, IRepository<ProductManufacturer> manufacturerMappings,
    IRepository<ProductSpecificationAttribute> specificationMappings)
{
    public async Task<SearchModel?> PrepareAsync(CatalogProductsCommand command, string? q, int cid, bool isc)
    {
        var storeId = (await store.GetCurrentStoreAsync()).Id;
        var languageId = (await work.GetWorkingLanguageAsync()).Id;
        var result = new SearchModel { q = q?.Trim() ?? "", cid = cid, isc = isc, advs = true };
        var availableCategories = await categories.GetAllCategoriesAsync(storeId);
        if (cid != 0 && availableCategories.All(category => category.Id != cid))
            return null;
        foreach (var category in availableCategories)
            result.AvailableCategories.Add(new SelectListItem(await localization.GetLocalizedAsync(category, item => item.Name), category.Id.ToString(), category.Id == cid));

        var model = result.CatalogProductsModel;
        await catalog.PrepareSortingOptionsAsync(model, command);
        if (model.AllowProductSorting && model.AvailableSortOptions.All(option => option.Value != command.OrderBy.ToString()))
            command.OrderBy = int.TryParse(model.AvailableSortOptions.FirstOrDefault()?.Value, out var order) ? order : 0;
        model.OrderBy = command.OrderBy;
        foreach (var option in model.AvailableSortOptions)
            option.Selected = option.Value == command.OrderBy.ToString();
        await catalog.PrepareViewModesAsync(model, command);
        model.ViewMode = settings.AllowProductViewModeChanging && command.ViewMode is "grid" or "list"
            ? command.ViewMode : settings.DefaultViewMode;
        foreach (var option in model.AvailableViewModes)
            option.Selected = option.Value == model.ViewMode;
        await catalog.PreparePageSizeOptionsAsync(model, command, settings.SearchPageAllowCustomersToSelectPageSize,
            settings.SearchPagePageSizeOptions, settings.SearchPageProductsPerPage);
        command.PageNumber = Math.Min(command.PageNumber, int.MaxValue / command.PageSize);

        var categoryIds = new List<int>();
        if (cid > 0)
        {
            categoryIds.Add(cid);
            if (isc)
                categoryIds.AddRange(await categories.GetChildCategoryIdsAsync(cid, storeId));
        }

        var now = DateTime.UtcNow;
        var scope = productRepository.Table.Where(product => product.Published && !product.Deleted && product.VisibleIndividually
            && (!product.AvailableStartDateTimeUtc.HasValue || product.AvailableStartDateTimeUtc <= now)
            && (!product.AvailableEndDateTimeUtc.HasValue || product.AvailableEndDateTimeUtc >= now));
        scope = await acl.ApplyAcl(scope, await work.GetCurrentCustomerAsync());
        scope = await stores.ApplyStoreMapping(scope, storeId);
        if (categoryIds.Count > 0)
            scope = scope.Where(product => categoryMappings.Table.Any(mapping => mapping.ProductId == product.Id && categoryIds.Contains(mapping.CategoryId)));

        var selectedManufacturers = new List<int>();
        if (settings.EnableManufacturerFiltering)
        {
            var ids = await manufacturerMappings.Table.Where(mapping => scope.Any(product => product.Id == mapping.ProductId))
                .Select(mapping => mapping.ManufacturerId).Distinct().ToListAsync();
            foreach (var manufacturer in (await manufacturers.GetAllManufacturersAsync(storeId: storeId)).Where(item => ids.Contains(item.Id)))
            {
                var selected = command.Ms?.Contains(manufacturer.Id) == true;
                model.ManufacturerFilter.Manufacturers.Add(new SelectListItem(await localization.GetLocalizedAsync(manufacturer, item => item.Name), manufacturer.Id.ToString(), selected));
                if (selected)
                    selectedManufacturers.Add(manufacturer.Id);
            }
            model.ManufacturerFilter.Enabled = model.ManufacturerFilter.Manufacturers.Count > 0;
        }

        var selectedSpecifications = new List<SpecificationAttributeOption>();
        if (settings.EnableSpecificationAttributeFiltering)
        {
            var ids = await specificationMappings.Table.Where(mapping => mapping.AllowFiltering && scope.Any(product => product.Id == mapping.ProductId))
                .Select(mapping => mapping.SpecificationAttributeOptionId).Distinct().ToListAsync();
            foreach (var option in await specifications.GetSpecificationAttributeOptionsByIdsAsync(ids.ToArray()))
            {
                var group = model.SpecificationFilter.Attributes.FirstOrDefault(item => item.Id == option.SpecificationAttributeId);
                if (group == null)
                {
                    var attribute = await specifications.GetSpecificationAttributeByIdAsync(option.SpecificationAttributeId);
                    if (attribute == null)
                        continue;
                    group = new SpecificationAttributeFilterModel { Id = attribute.Id, Name = await localization.GetLocalizedAsync(attribute, item => item.Name) };
                    model.SpecificationFilter.Attributes.Add(group);
                }
                var selected = command.Specs?.Contains(option.Id) == true;
                group.Values.Add(new SpecificationAttributeValueFilterModel
                {
                    Id = option.Id,
                    Name = await localization.GetLocalizedAsync(option, item => item.Name),
                    Selected = selected,
                    ColorSquaresRgb = option.ColorSquaresRgb
                });
                if (selected)
                    selectedSpecifications.Add(option);
            }
            model.SpecificationFilter.Enabled = model.SpecificationFilter.Attributes.Count > 0;
        }

        if (result.q.Length > 0 && result.q.Length < settings.ProductSearchTermMinimumLength)
        {
            model.WarningMessage = string.Format(await localization.GetResourceAsync("Search.SearchTermMinimumLengthIsNCharacters"), settings.ProductSearchTermMinimumLength);
            return result;
        }

        var selectedPrice = new PriceRangeModel();
        if (settings.EnablePriceRangeFiltering && settings.SearchPagePriceRangeFiltering)
        {
            selectedPrice = await ParsePriceAsync(command.Price);
            var lowest = await products.SearchProductsAsync(0, 1, categoryIds: categoryIds, storeId: storeId,
                visibleIndividuallyOnly: true, keywords: result.q, languageId: languageId, orderBy: ProductSortingEnum.PriceAsc);
            var highest = await products.SearchProductsAsync(0, 1, categoryIds: categoryIds, storeId: storeId,
                visibleIndividuallyOnly: true, keywords: result.q, languageId: languageId, orderBy: ProductSortingEnum.PriceDesc);
            var available = settings.SearchPageManuallyPriceRange
                ? new PriceRangeModel { From = settings.SearchPagePriceFrom, To = highest.Count == 0 ? 0 : settings.SearchPagePriceTo }
                : new PriceRangeModel { From = lowest.FirstOrDefault()?.Price ?? 0, To = highest.FirstOrDefault()?.Price ?? 0 };
            model.PriceRangeFilter = await PreparePriceAsync(selectedPrice, available);
        }

        var page = await products.SearchProductsAsync(command.PageNumber - 1, command.PageSize,
            categoryIds: categoryIds, manufacturerIds: selectedManufacturers, storeId: storeId,
            visibleIndividuallyOnly: true, keywords: result.q, languageId: languageId,
            priceMin: selectedPrice.From, priceMax: selectedPrice.To, filteredSpecOptions: selectedSpecifications,
            orderBy: (ProductSortingEnum)(command.OrderBy ?? 0));
        model.LoadPagedList(page);
        model.Products = (await overview.PrepareProductOverviewModelsAsync(page)).ToList();
        if (page.Count == 0)
            model.NoResultMessage = await localization.GetResourceAsync("Catalog.Products.NoResult");
        return result;
    }

    // These two helpers mirror the protected native currency and range preparation.
    private async Task<PriceRangeModel> ParsePriceAsync(string? price)
    {
        var result = new PriceRangeModel();
        if (string.IsNullOrWhiteSpace(price))
            return result;
        var parts = price.Trim().Split('-');
        if (parts.Length != 2)
            return result;
        if (decimal.TryParse(parts[0].Trim(), out var from))
            result.From = from;
        if (decimal.TryParse(parts[1].Trim(), out var to))
            result.To = to;
        if (result.From > result.To)
            result.From = result.To;
        var currency = await work.GetWorkingCurrencyAsync();
        if (result.From.HasValue)
            result.From = await currencies.ConvertToPrimaryStoreCurrencyAsync(result.From.Value, currency);
        if (result.To.HasValue)
            result.To = await currencies.ConvertToPrimaryStoreCurrencyAsync(result.To.Value, currency);
        return result;
    }

    private async Task<PriceRangeFilterModel> PreparePriceAsync(PriceRangeModel selected, PriceRangeModel available)
    {
        var result = new PriceRangeFilterModel();
        if (!available.To.HasValue || available.To <= 0 || available.To == available.From)
        {
            selected.From = selected.To = null;
            return result;
        }
        if (selected.From < available.From)
            selected.From = available.From;
        if (selected.To > available.To)
            selected.To = available.To;
        var currency = await work.GetWorkingCurrencyAsync();
        result.Enabled = true;
        result.AvailablePriceRange.From = available.From > 0
            ? Math.Floor(await currencies.ConvertFromPrimaryStoreCurrencyAsync(available.From.Value, currency)) : 0;
        result.AvailablePriceRange.To = Math.Ceiling(await currencies.ConvertFromPrimaryStoreCurrencyAsync(available.To.Value, currency));
        result.SelectedPriceRange.From = !selected.From.HasValue || selected.From == available.From
            ? result.AvailablePriceRange.From : selected.From > 0
                ? Math.Floor(await currencies.ConvertFromPrimaryStoreCurrencyAsync(selected.From.Value, currency)) : null;
        result.SelectedPriceRange.To = !selected.To.HasValue || selected.To == available.To
            ? result.AvailablePriceRange.To : selected.To > 0
                ? Math.Ceiling(await currencies.ConvertFromPrimaryStoreCurrencyAsync(selected.To.Value, currency)) : null;
        return result;
    }
}