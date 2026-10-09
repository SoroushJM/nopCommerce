using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Core.Http;
using Nop.Services.Localization;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Catalog;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public static class NativeProductPresentation
{
    public static bool Supports(ProductDetailsModel model)
    {
        return model.ProductType == ProductType.SimpleProduct
            && !model.IsRental && !model.GiftCard.IsGiftCard && !model.AddToCart.CustomerEntersPrice
            && model.AddToCart.UpdateShoppingCartItemType != ShoppingCartType.Wishlist
            && model.ProductAttributes.All(attribute => !attribute.Values.Any(value => value.CustomerEntersQty)
                && attribute.AttributeControlType is AttributeControlType.ColorSquares
                    or AttributeControlType.DropdownList or AttributeControlType.RadioList or AttributeControlType.ImageSquares);
    }

    public static async Task<NativeProduct> FromAsync(ProductDetailsModel model, IUrlHelper url,
        INopUrlHelper nopUrl, ILocalizationService localization, IWorkContext workContext, int minimumQuantity)
    {
        var breadcrumb = new List<StoreLink>();
        foreach (var category in model.Breadcrumb.CategoryBreadcrumb)
            breadcrumb.Add(new StoreLink(category.Id, category.Name,
                await nopUrl.RouteGenericUrlAsync<Category>(new
                {
                    SeName = category.SeName
                })));
        var manufacturers = new List<StoreLink>();
        foreach (var manufacturer in model.ProductManufacturers)
            manufacturers.Add(new StoreLink(manufacturer.Id, manufacturer.Name,
                await nopUrl.RouteGenericUrlAsync<Manufacturer>(new
                {
                    SeName = manufacturer.SeName
                })));
        var taxShippingInfo = "";
        if (model.ProductPrice.DisplayTaxShippingInfo && !model.ProductPrice.CallForPrice && !model.ProductPrice.HidePrices)
        {
            var inclTax = await workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax;
            taxShippingInfo = string.Format(await localization.GetResourceAsync(inclTax
                ? "Products.Price.TaxShipping.InclTax" : "Products.Price.TaxShipping.ExclTax"),
                await nopUrl.RouteTopicUrlAsync("shippinginfo"));
        }
        var purchaseResource = model.AddToCart.UpdatedShoppingCartItemId > 0
            ? "ShoppingCart.AddToCart.Update" : model.AddToCart.AvailableForPreOrder
                ? "ShoppingCart.PreOrder" : "ShoppingCart.AddToCart";
        return new NativeProduct
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.ShortDescription ?? "",
            Image = model.DefaultPictureModel.ImageUrl,
            ImageAlt = model.DefaultPictureModel.AlternateText,
            FullSizeImage = model.DefaultPictureModel.FullSizeImageUrl,
            ImageTitle = model.DefaultPictureModel.Title,
            ZoomEnabled = model.DefaultPictureZoomEnabled,
            CombinationImagesOnly = model.DisplayAttributeCombinationImagesOnly,
            Pictures = model.PictureModels.Select(picture => new NativeProductPicture(picture.Id, picture.ImageUrl,
                picture.ThumbImageUrl, picture.FullSizeImageUrl, picture.AlternateText, picture.Title)).ToList(),
            Price = string.IsNullOrWhiteSpace(model.ProductPrice.PriceWithDiscount)
                ? model.ProductPrice.Price : model.ProductPrice.PriceWithDiscount,
            ReferencePrice = string.IsNullOrWhiteSpace(model.ProductPrice.PriceWithDiscount) ? "" : model.ProductPrice.Price,
            OldPrice = model.ProductPrice.OldPrice,
            BasePrice = model.ProductPrice.BasePricePAngV,
            HidePrices = model.ProductPrice.HidePrices,
            CallForPrice = model.ProductPrice.CallForPrice,
            CallForPriceLabel = await localization.GetResourceAsync("Products.CallForPrice"),
            TaxShippingInfo = taxShippingInfo,
            TierPrices = model.TierPrices.Select(tier => new NativeTierPrice(tier.Quantity, tier.Price)).ToList(),
            Stock = model.StockAvailability,
            InStock = model.InStock,
            DisableBuy = model.AddToCart.DisableBuyButton,
            ExistingCombinationsOnly = model.AllowAddingOnlyExistingAttributeCombinations,
            StockByAttributes = model.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes,
            Quantity = model.AddToCart.EnteredQuantity,
            MinimumQuantity = minimumQuantity,
            MinimumQuantityNotification = model.AddToCart.MinimumQuantityNotification ?? "",
            PurchaseLabel = await localization.GetResourceAsync(purchaseResource),
            PreOrderDate = model.AddToCart.PreOrderAvailabilityStartDateTimeUserTime ?? "",
            Sku = model.Sku ?? "",
            ShowSku = model.ShowSku,
            Mpn = model.ManufacturerPartNumber ?? "",
            ShowMpn = model.ShowManufacturerPartNumber,
            Gtin = model.Gtin ?? "",
            ShowGtin = model.ShowGtin,
            Manufacturers = manufacturers,
            ShowVendor = model.ShowVendor,
            Vendor = model.VendorModel.Id > 0 ? new StoreLink(model.VendorModel.Id, model.VendorModel.Name,
                await nopUrl.RouteGenericUrlAsync<Vendor>(new
                {
                    SeName = model.VendorModel.SeName
                })) : null,
            IsShipEnabled = model.IsShipEnabled,
            IsFreeShipping = model.IsFreeShipping,
            FreeShippingNotificationEnabled = model.FreeShippingNotificationEnabled,
            DeliveryDate = model.DeliveryDate ?? "",
            CartItemId = model.AddToCart.UpdatedShoppingCartItemId,
            Quantities = model.AddToCart.AllowedQuantities.Select(item => int.Parse(item.Value)).ToList(),
            Brand = model.ProductManufacturers.FirstOrDefault()?.Name ?? "",
            BreadcrumbEnabled = model.Breadcrumb.Enabled,
            Breadcrumb = breadcrumb,
            Attributes = model.ProductAttributes.Select(attribute => new NativeAttribute(
                attribute.Id, attribute.Name, (int)attribute.AttributeControlType, attribute.IsRequired, attribute.HasCondition,
                attribute.Values.Select(value => new NativeOption(value.Id, value.Name, value.ColorSquaresRgb,
                    value.ImageSquaresPictureModel.ImageUrl, value.IsPreSelected)).ToList())).ToList(),
            ChangeUrl = url.RouteUrl(NopRouteNames.Ajax.PRODUCT_DETAILS_ATTRIBUTE_CHANGE,
                new
                {
                    productId = model.Id,
                    validateAttributeConditions = true,
                    loadPicture = true
                })!,
            AddUrl = url.RouteUrl(NopRouteNames.Ajax.ADD_PRODUCT_TO_CART_DETAILS,
                new
                {
                    productId = model.Id,
                    shoppingCartTypeId = (int)ShoppingCartType.ShoppingCart
                })!,
            CombinationsUrl = url.RouteUrl(NopRouteNames.Ajax.GET_PRODUCT_COMBINATIONS, new { productId = model.Id })!
        };
    }
}