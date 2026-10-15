using System.Globalization;
using System.Text.Json.Serialization;

namespace Storefront.UI.Shared;

/// <summary>Plain presentation text and direction prepared for the working store language.</summary>
public sealed record NativeUiLabels
{
    public bool Rtl { get; init; } = true;
    public Dictionary<string, string> Values { get; init; } = [];
    [JsonIgnore] public string Direction => Rtl ? "rtl" : "ltr";

    public string Get(string resource) => Values.TryGetValue(resource, out var value)
        ? value : Defaults.GetValueOrDefault(resource, resource);

    public string Format(string resource, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(resource), arguments);

    // Backward compatible Persian presentation until server-prepared labels arrive.
    public static IReadOnlyDictionary<string, string> Defaults { get; } = new Dictionary<string, string>
    {
        ["Homepage"] = "خانه", ["Homepage.Products"] = "فروشگاه", ["Menu"] = "منو",
        ["Account.MyAccount"] = "حساب من", ["Account.Login"] = "ورود / عضویت", ["Account.Logout"] = "خروج از حساب",
        ["Account.Register"] = "ثبت‌نام", ["Wishlist"] = "علاقه‌مندی‌ها", ["Wishlist.HeaderQuantity"] = "({0})",
        ["ShoppingCart"] = "سبد خرید", ["ShoppingCart.HeaderQuantity"] = "({0})",
        ["ShoppingCart.CartIsEmpty"] = "سبدت هنوز خالیه.", ["ShoppingCart.ContinueShopping"] = "ادامهٔ خرید",
        ["ShoppingCart.Mini.ViewCart"] = "دیدن سبد و ادامهٔ خرید", ["Checkout.Button"] = "ادامهٔ خرید",
        ["ShoppingCart.Totals.SubTotal"] = "جمع کالاها", ["ShoppingCart.Remove"] = "حذف",
        ["ShoppingCart.Mini.UnitPrice"] = "هر عدد", ["ShoppingCart.Totals.SubTotalDiscount"] = "تخفیف",
        ["Common.Edit"] = "ویرایش انتخاب‌ها", ["Common.Error"] = "درخواست انجام نشد. دوباره تلاش کنید.",
        ["Common.Close"] = "بستن", ["Common.Wait"] = "لطفاً صبر کنید...",
        ["Products.Qty"] = "تعداد", ["Products.Qty.AriaLabel"] = "تعداد محصول",
        ["Products.QuantityShouldBePositive"] = "تعداد باید عدد صحیح و مثبت باشد.",
        ["Products.Sku"] = "کد محصول", ["Products.ManufacturerPartNumber"] = "کد تولیدکننده",
        ["Products.GTIN"] = "شناسهٔ کالا", ["Products.Vendor"] = "فروشنده",
        ["Products.FreeShipping"] = "ارسال رایگان", ["Products.DeliveryDate"] = "زمان تحویل",
        ["Products.Price"] = "قیمت هر عدد", ["Products.TierPrices"] = "قیمت خرید تعدادی",
        ["Products.TierPrices.Quantity"] = "تعداد", ["Products.TierPrices.Price"] = "قیمت هر عدد",
        ["Products.Availability.OutOfStock"] = "فعلاً ناموجود",
        ["Products.ProductAttributes.DropdownList.DefaultItem"] = "انتخاب کنید",
        ["ShoppingCart.PreOrderAvailability"] = "زمان عرضهٔ پیش‌سفارش",
        ["Media.Product.ImageAlternateTextFormat.Details"] = "تصویر {0}",
        ["Media.Product.ImageLinkTitleFormat.Details"] = "تصویر {0}",
        ["Catalog.OrderBy"] = "مرتب‌سازی", ["Catalog.OrderBy.Label"] = "مرتب‌سازی محصولات",
        ["Catalog.PageSize.Label"] = "تعداد محصول در صفحه", ["Catalog.ViewMode"] = "نمایش شبکه‌ای یا فهرستی",
        ["Categories"] = "دسته‌بندی‌ها", ["Search.Category"] = "دسته‌بندی محصولات",
        ["Search.Manufacturer"] = "برند", ["Filtering.ManufacturerFilter"] = "فیلتر برند",
        ["Filtering.PriceRangeFilter"] = "بازهٔ قیمت", ["Filtering.SpecificationFilter"] = "فیلتر محصولات",
        ["Common.All"] = "همه", ["Common.Confirm"] = "اعمال", ["Common.Remove"] = "پاک کردن"
    };
}
