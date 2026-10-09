using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core.Http;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.ShoppingCart;
using Storefront.UI;

namespace Nop.Plugin.Misc.PersianStorefront;

public sealed class NativeCartPresentation(IWorkContext work, IStoreContext stores,
    ICustomerService customers, IShoppingCartService carts, IShoppingCartModelFactory factory,
    ShoppingCartSettings settings, OrderSettings orders, INopUrlHelper nopUrl, ILocalizationService localization)
{
    public async Task<NativeCart> FromAsync(ShoppingCartModel model, IUrlHelper urls, AntiforgeryTokenSet token)
    {
        var customer = await work.GetCurrentCustomerAsync();
        var store = await stores.GetCurrentStoreAsync();
        var cart = await carts.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);
        var mini = await factory.PrepareMiniShoppingCartModelAsync();
        var totals = await factory.PrepareOrderTotalsModelAsync(cart, true);
        var lines = new List<NativeCartLine>();
        var items = model.Items.ToDictionary(item => item.Id);
        foreach (var miniItem in mini.Items)
        {
            if (!items.TryGetValue(miniItem.Id, out var item))
                continue;
            var url = await nopUrl.RouteGenericUrlAsync<Product>(new
            {
                SeName = item.ProductSeName
            });
            lines.Add(new NativeCartLine
            {
                Id = item.Id,
                Name = item.ProductName,
                Url = url,
                EditUrl = item.AllowItemEditing ? QueryHelpers.AddQueryString(url, "updatecartitemid", item.Id.ToString(CultureInfo.InvariantCulture)) : "",
                Image = mini.ShowProductImages ? miniItem.Picture.ImageUrl : "",
                ImageAlt = miniItem.Picture.AlternateText ?? "",
                ImageTitle = miniItem.Picture.Title ?? "",
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice ?? "",
                SubTotal = item.SubTotal ?? "",
                Discount = model.ShowItemDiscount ? item.Discount ?? "" : "",
                DiscountNote = model.ShowItemDiscount && item.MaximumDiscountedQty.HasValue
                    ? string.Format(await localization.GetResourceAsync("ShoppingCart.MaximumDiscountedQty"), item.MaximumDiscountedQty.Value) : "",
                AttributeInfo = item.AttributeInfo ?? "",
                RecurringInfo = item.RecurringInfo ?? "",
                RentalInfo = item.RentalInfo ?? "",
                DisableRemoval = item.DisableRemoval,
                AllowedQuantities = item.AllowedQuantities.Select(option => new CatalogChoice(option.Value, option.Text, option.Selected)).ToList()
            });
        }
        var cartUrl = urls.RouteUrl(NopRouteNames.General.CART)!;
        return new NativeCart
        {
            MiniEnabled = settings.MiniShoppingCartEnabled,
            SignedIn = await customers.IsRegisteredAsync(customer),
            Phone = customer.Phone ?? "",
            Count = mini.TotalProducts,
            CartUrl = cartUrl,
            CheckoutUrl = mini.AnonymousCheckoutAllowed && mini.CurrentCustomerIsGuest
                ? urls.RouteUrl(NopRouteNames.Standard.LOGIN_CHECKOUT_AS_GUEST, new
                {
                    returnUrl = cartUrl
                })!
                : urls.RouteUrl(NopRouteNames.Standard.CHECKOUT)!,
            DisplayShoppingCartButton = mini.DisplayShoppingCartButton,
            DisplayCheckoutButton = mini.DisplayCheckoutButton && !model.HideCheckoutButton && !orders.CheckoutDisabled,
            ShowProductImages = mini.ShowProductImages,
            MiniSubTotal = mini.SubTotal ?? "",
            Lines = lines,
            Warnings = model.Warnings.Concat(string.IsNullOrEmpty(model.MinOrderSubtotalWarning) ? [] : new[] { model.MinOrderSubtotalWarning }).ToList(),
            ItemWarnings = model.Items.Where(item => item.Warnings.Count > 0)
                .Select(item => new NativeCartWarning(item.Id, item.ProductName, item.Warnings.ToList())).ToList(),
            Totals = await TotalRowsAsync(totals),
            CheckoutFields = CheckoutFields(model),
            TokenField = token.FormFieldName,
            Token = token.RequestToken ?? ""
        };
    }

    private static List<NativeFormField> CheckoutFields(ShoppingCartModel model)
    {
        var fields = new List<NativeFormField>();
        foreach (var attribute in model.CheckoutAttributes)
        {
            var name = "checkout_attribute_" + attribute.Id;
            switch (attribute.AttributeControlType)
            {
                case AttributeControlType.DropdownList:
                case AttributeControlType.RadioList:
                case AttributeControlType.ColorSquares:
                case AttributeControlType.ImageSquares:
                case AttributeControlType.Checkboxes:
                case AttributeControlType.ReadonlyCheckboxes:
                    fields.Add(new(name, attribute.Values.Where(value => value.IsPreSelected)
                        .Select(value => value.Id.ToString(CultureInfo.InvariantCulture)).ToList()));
                    break;
                case AttributeControlType.Datepicker:
                    fields.Add(new(name + "_day", [attribute.SelectedDay?.ToString(CultureInfo.InvariantCulture) ?? ""]));
                    fields.Add(new(name + "_month", [attribute.SelectedMonth?.ToString(CultureInfo.InvariantCulture) ?? ""]));
                    fields.Add(new(name + "_year", [attribute.SelectedYear?.ToString(CultureInfo.InvariantCulture) ?? ""]));
                    break;
                case AttributeControlType.TextBox:
                case AttributeControlType.MultilineTextbox:
                case AttributeControlType.FileUpload:
                    fields.Add(new(name, [attribute.DefaultValue ?? ""]));
                    break;
            }
        }
        return fields;
    }

    private async Task<List<NativeCartTotalRow>> TotalRowsAsync(OrderTotalsModel totals)
    {
        var rows = new List<NativeCartTotalRow>();
        async Task Add(string key, string value, string detail = "", bool emphasis = false)
        {
            rows.Add(new(await localization.GetResourceAsync(key), value, detail, emphasis));
        }
        await Add("ShoppingCart.Totals.SubTotal", totals.SubTotal ?? "");
        if (!string.IsNullOrEmpty(totals.SubTotalDiscount))
            await Add("ShoppingCart.Totals.SubTotalDiscount", totals.SubTotalDiscount);
        if (!totals.HideShippingTotal)
        {
            var shipping = totals.RequiresShipping
                ? totals.Shipping ?? "" : await localization.GetResourceAsync("ShoppingCart.Totals.Shipping.NotRequired");
            if (totals.RequiresShipping && string.IsNullOrEmpty(shipping))
                shipping = await localization.GetResourceAsync("ShoppingCart.Totals.CalculatedDuringCheckout");
            var method = totals.RequiresShipping && !string.IsNullOrEmpty(totals.SelectedShippingMethod)
                ? string.Format(await localization.GetResourceAsync("ShoppingCart.Totals.Shipping.Method"), totals.SelectedShippingMethod) : "";
            await Add("ShoppingCart.Totals.Shipping", shipping, method);
        }
        if (!string.IsNullOrEmpty(totals.PaymentMethodAdditionalFee))
            await Add("ShoppingCart.Totals.PaymentMethodAdditionalFee", totals.PaymentMethodAdditionalFee);
        if (totals.DisplayTaxRates && totals.TaxRates.Count > 0)
        {
            foreach (var tax in totals.TaxRates)
                rows.Add(new(string.Format(await localization.GetResourceAsync("ShoppingCart.Totals.TaxRateLine"), tax.Rate), tax.Value));
        }
        if (totals.DisplayTax)
            await Add("ShoppingCart.Totals.Tax", totals.Tax ?? "");
        if (!string.IsNullOrEmpty(totals.OrderTotalDiscount))
            await Add("ShoppingCart.Totals.OrderTotalDiscount", totals.OrderTotalDiscount);
        foreach (var card in totals.GiftCards)
        {
            var detail = string.Format(await localization.GetResourceAsync("ShoppingCart.Totals.GiftCardInfo.Code"), card.CouponCode)
                + " · " + string.Format(await localization.GetResourceAsync("ShoppingCart.Totals.GiftCardInfo.Remaining"), card.Remaining);
            await Add("ShoppingCart.Totals.GiftCardInfo", card.Amount, detail);
        }
        if (totals.RedeemedRewardPoints > 0)
            rows.Add(new(string.Format(await localization.GetResourceAsync("ShoppingCart.Totals.RewardPoints"), totals.RedeemedRewardPoints), totals.RedeemedRewardPointsAmount));
        await Add("ShoppingCart.Totals.OrderTotal", string.IsNullOrEmpty(totals.OrderTotal)
            ? await localization.GetResourceAsync("ShoppingCart.Totals.CalculatedDuringCheckout") : totals.OrderTotal, emphasis: true);
        if (totals.WillEarnRewardPoints > 0)
            await Add("ShoppingCart.Totals.RewardPoints.WillEarn", string.Format(await localization.GetResourceAsync("ShoppingCart.Totals.RewardPoints.WillEarn.Point"), totals.WillEarnRewardPoints));
        return rows;
    }
}