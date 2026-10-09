using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Web.Models.Catalog;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public static class NativeProductPresentation
{
    public static bool Supports(ProductDetailsModel model)
    {
        return !model.IsRental && !model.GiftCard.IsGiftCard && !model.AddToCart.CustomerEntersPrice
            && !model.ProductPrice.HidePrices && !model.ProductPrice.CallForPrice
            && model.ProductAttributes.All(attribute => !attribute.Values.Any(value => value.CustomerEntersQty)
                && attribute.AttributeControlType is AttributeControlType.ColorSquares
                    or AttributeControlType.DropdownList or AttributeControlType.RadioList or AttributeControlType.ImageSquares);
    }

    public static NativeProduct From(ProductDetailsModel model, IUrlHelper url)
    {
        return new NativeProduct
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.ShortDescription ?? "",
            Image = model.DefaultPictureModel.ImageUrl,
            ImageAlt = model.DefaultPictureModel.AlternateText,
            Price = model.ProductPrice.Price,
            OldPrice = model.ProductPrice.OldPrice,
            Stock = model.StockAvailability,
            InStock = model.InStock,
            DisableBuy = model.AddToCart.DisableBuyButton,
            ExistingCombinationsOnly = model.AllowAddingOnlyExistingAttributeCombinations,
            StockByAttributes = model.ManageInventoryMethod == ManageInventoryMethod.ManageStockByAttributes,
            Quantity = model.AddToCart.EnteredQuantity,
            CartItemId = model.AddToCart.UpdatedShoppingCartItemId,
            Quantities = model.AddToCart.AllowedQuantities.Select(item => int.Parse(item.Value)).ToList(),
            Brand = model.ProductManufacturers.FirstOrDefault()?.Name ?? "",
            Breadcrumb = model.Breadcrumb.CategoryBreadcrumb.Select(category =>
                new StoreLink(category.Id, category.Name, url.Content("~/" + category.SeName))).ToList(),
            Attributes = model.ProductAttributes.Select(attribute => new NativeAttribute(
                attribute.Id, attribute.Name, (int)attribute.AttributeControlType, attribute.IsRequired, attribute.HasCondition,
                attribute.Values.Select(value => new NativeOption(value.Id, value.Name, value.ColorSquaresRgb,
                    value.ImageSquaresPictureModel.ImageUrl, value.IsPreSelected)).ToList())).ToList(),
            ChangeUrl = url.RouteUrl(NopRouteNames.Ajax.PRODUCT_DETAILS_ATTRIBUTE_CHANGE,
                new { productId = model.Id, validateAttributeConditions = true, loadPicture = true })!,
            AddUrl = url.RouteUrl(NopRouteNames.Ajax.ADD_PRODUCT_TO_CART_DETAILS,
                new { productId = model.Id, shoppingCartTypeId = (int)ShoppingCartType.ShoppingCart })!,
            CombinationsUrl = url.RouteUrl(NopRouteNames.Ajax.GET_PRODUCT_COMBINATIONS, new { productId = model.Id })!
        };
    }
}