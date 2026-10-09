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
            ["Products.Availability.InStock"] = "موجود",
            ["Products.Availability.InStockWithQuantity"] = "{0} عدد موجود",
            ["Products.Availability.OutOfStock"] = "ناموجود",
            ["Products.MinimumQuantityNotification"] = "حداقل تعداد خرید این کالا {0} عدد است.",
            ["Products.CallForPrice"] = "برای قیمت تماس بگیر",
            ["Products.Specs"] = "مشخصات محصول",
            ["Products.Specs.AttributeName"] = "ویژگی",
            ["Products.Specs.AttributeValue"] = "مشخصات",
            ["Products.Tags"] = "برچسب‌های محصول",
            ["Products.AlsoPurchased"] = "همراه این کالا خریده‌اند",
            ["Products.RelatedProducts"] = "انتخاب‌های مشابه",
            ["Products.DownloadSample"] = "دریافت نمونه",
            ["Products.Discontinued"] = "عرضهٔ این کالا متوقف شده است.",
            ["Products.EstimateShipping.PriceTitle"] = "هزینهٔ ارسال:",
            ["Products.EstimateShipping.ToAddress"] = "به مقصد",
            ["Products.EstimateShipping.ViaProvider"] = "با",
            ["Products.EstimateShipping.EstimatedDeliveryPrefix"] = "تحویل حدودی:",
            ["Products.EstimateShipping.NoSelectedShippingOption"] = "مقصد ارسال را انتخاب کن",
            ["BackInStockSubscriptions.NotifyMeWhenAvailable"] = "وقتی موجود شد خبرم کن",
            ["ShoppingCart.AddToCart.Update"] = "به‌روزرسانی سبد خرید",
            ["ShoppingCart.AddToCart"] = "افزودن به سبد خرید",
            ["ShoppingCart.PreOrder"] = "پیش‌سفارش",
            ["ShoppingCart.PreOrderAvailability"] = "زمان عرضه",
            ["Reviews.Overview.Reviews"] = "نظر",
            ["Reviews.Overview.First"] = "اولین نظر را بنویس",
            ["Reviews.Overview.AddNew"] = "نظر تو دربارهٔ این کالا",
            ["Reviews.ExistingReviews"] = "نظرهای خریداران",
            ["Reviews.Write"] = "نظرت را بنویس",
            ["Reviews.From"] = "نویسنده",
            ["Reviews.Date"] = "تاریخ",
            ["Reviews.Reply"] = "پاسخ فروشگاه",
            ["Reviews.OnlyRegisteredUsersCanWriteReviews"] = "برای نوشتن نظر وارد حساب شو.",
            ["Reviews.AlreadyAddedProductReviews"] = "نظرت برای این کالا ثبت شده است.",
            ["Reviews.Fields.Title.Required"] = "عنوان نظر را وارد کن.",
            ["Reviews.Fields.Title.MaxLengthValidation"] = "عنوان نظر باید حداکثر {0} نویسه داشته باشد.",
            ["Reviews.Fields.ReviewText.Required"] = "متن نظر را وارد کن.",
            ["Reviews.Fields.Title"] = "عنوان نظر",
            ["Reviews.Fields.ReviewText"] = "متن نظر",
            ["Reviews.Fields.Rating"] = "امتیاز",
            ["Reviews.Fields.Rating.Bad"] = "ضعیف",
            ["Reviews.Fields.Rating.Good"] = "خوب",
            ["Reviews.Fields.Rating.NotGood"] = "متوسط رو به پایین",
            ["Reviews.Fields.Rating.NotBadNotExcellent"] = "متوسط",
            ["Reviews.Fields.Rating.Excellent"] = "عالی",
            ["Reviews.SubmitButton"] = "ثبت نظر",
            ["Categories.Breadcrumb.Top"] = "خانه",
            ["PageTitle.ShoppingCart"] = "سبد خرید",
            ["ShoppingCart"] = "سبد خرید",
            ["ShoppingCart.CartIsEmpty"] = "سبدت هنوز خالیه.",
            ["ShoppingCart.SKU"] = "کد کالا",
            ["ShoppingCart.VendorList"] = "فروشنده",
            ["ShoppingCart.UpdateCart"] = "به‌روزرسانی سبد",
            ["ShoppingCart.ContinueShopping"] = "ادامهٔ انتخاب کالا",
            ["ShoppingCart.ItemYouSave"] = "صرفه‌جویی: {0}",
            ["ShoppingCart.MaximumDiscountedQty"] = "تعداد مشمول تخفیف: {0}",
            ["ShoppingCart.MaximumQuantity"] = "حداکثر تعداد قابل خرید {0} عدد است.",
            ["ShoppingCart.MinimumQuantity"] = "حداقل تعداد قابل خرید {0} عدد است.",
            ["ShoppingCart.QuantityExceedsStock"] = "تعداد درخواستی بیشتر از موجودی است. حداکثر {0} عدد می‌توانی اضافه کنی.",
            ["ShoppingCart.QuantityShouldPositive"] = "تعداد باید مثبت باشد.",
            ["ShoppingCart.OutOfStock"] = "ناموجود",
            ["ShoppingCart.NotAvailable"] = "این کالا در دسترس نیست.",
            ["ShoppingCart.AllowedQuantities"] = "تعداد مجاز: {0}",
            ["ShoppingCart.DiscountCouponCode"] = "کد تخفیف",
            ["ShoppingCart.DiscountCouponCode.Tooltip"] = "اگر کد تخفیف داری، اینجا بنویس.",
            ["ShoppingCart.DiscountCouponCode.Label"] = "کد تخفیف",
            ["ShoppingCart.DiscountCouponCode.Button"] = "اعمال",
            ["ShoppingCart.DiscountCouponCode.CurrentCode"] = "کد فعال: {0}",
            ["ShoppingCart.DiscountCouponCode.CannotBeFound"] = "این کد تخفیف پیدا نشد.",
            ["ShoppingCart.DiscountCouponCode.Empty"] = "کد تخفیف را بنویس.",
            ["ShoppingCart.DiscountCouponCode.WrongDiscount"] = "این کد تخفیف برای سبد تو قابل استفاده نیست.",
            ["ShoppingCart.DiscountCouponCode.Applied"] = "کد تخفیف اعمال شد.",
            ["ShoppingCart.GiftCardCouponCode"] = "کارت هدیه",
            ["ShoppingCart.GiftCardCouponCode.Tooltip"] = "کد کارت هدیه را اینجا بنویس.",
            ["ShoppingCart.GiftCardCouponCode.Label"] = "کد کارت هدیه",
            ["ShoppingCart.GiftCardCouponCode.Button"] = "اعمال",
            ["ShoppingCart.GiftCardCouponCode.CannotBeFound"] = "این کارت هدیه پیدا نشد.",
            ["ShoppingCart.GiftCardCouponCode.Empty"] = "کد کارت هدیه را بنویس.",
            ["ShoppingCart.GiftCardCouponCode.WrongGiftCard"] = "این کارت هدیه برای سبد تو قابل استفاده نیست.",
            ["ShoppingCart.GiftCardCouponCode.Applied"] = "کارت هدیه اعمال شد.",
            ["ShoppingCart.Totals.SubTotal"] = "جمع کالاها",
            ["ShoppingCart.Totals.SubTotalDiscount"] = "تخفیف کالاها",
            ["ShoppingCart.Totals.Shipping"] = "ارسال",
            ["ShoppingCart.Totals.Shipping.Method"] = "روش ارسال: {0}",
            ["ShoppingCart.Totals.Shipping.NotRequired"] = "نیازی به ارسال ندارد",
            ["ShoppingCart.Totals.CalculatedDuringCheckout"] = "در ادامهٔ خرید محاسبه می‌شود",
            ["ShoppingCart.Totals.PaymentMethodAdditionalFee"] = "هزینهٔ روش پرداخت",
            ["ShoppingCart.Totals.Tax"] = "مالیات",
            ["ShoppingCart.Totals.TaxRateLine"] = "مالیات {0}٪",
            ["ShoppingCart.Totals.OrderTotalDiscount"] = "تخفیف سفارش",
            ["ShoppingCart.Totals.OrderTotal"] = "جمع نهایی",
            ["ShoppingCart.Totals.GiftCardInfo"] = "کارت هدیه",
            ["ShoppingCart.Totals.GiftCardInfo.Code"] = "کد: {0}",
            ["ShoppingCart.Totals.GiftCardInfo.Remaining"] = "مانده: {0}",
            ["ShoppingCart.Totals.RewardPoints"] = "استفاده از {0} امتیاز",
            ["ShoppingCart.Totals.RewardPoints.WillEarn"] = "امتیاز این خرید",
            ["ShoppingCart.Totals.RewardPoints.WillEarn.Point"] = "{0} امتیاز",
            ["ShoppingCart.EstimateShipping.Button"] = "برآورد هزینهٔ ارسال",
            ["ShoppingCart.CheckoutAttributes.PriceAdjustment"] = "{0} [{1}]",
            ["FormattedAttributes.PriceAdjustment"] = " [{0}{1}{2}]",
            ["Checkout.Button"] = "ادامهٔ خرید",
            ["Checkout.Disabled"] = "ثبت سفارش در حال حاضر غیرفعال است.",
            ["Checkout.TermsOfService"] = "شرایط خرید",
            ["Checkout.TermsOfService.PleaseAccept"] = "برای ادامه، شرایط خرید را بپذیر.",
            ["Checkout.TermsOfService.IAccept"] = "شرایط خرید را می‌پذیرم.",
            ["Checkout.TermsOfService.Read"] = "خواندن شرایط",
            ["Checkout.Progress.Cart"] = "سبد خرید",
            ["Checkout.Progress.Address"] = "نشانی",
            ["Checkout.Progress.Shipping"] = "ارسال",
            ["Checkout.Progress.Payment"] = "پرداخت",
            ["Checkout.Progress.Confirm"] = "تأیید",
            ["Checkout.Progress.Complete"] = "پایان",
            ["Shipping.EstimateShippingPopUp.ShipToTitle"] = "مقصد ارسال",
            ["Shipping.EstimateShippingPopUp.ChooseShippingTitle"] = "روش ارسال را انتخاب کن",
            ["Shipping.EstimateShippingPopUp.Country"] = "کشور",
            ["Shipping.EstimateShippingPopUp.StateProvince"] = "استان",
            ["Shipping.EstimateShippingPopUp.City"] = "شهر",
            ["Shipping.EstimateShippingPopUp.ZipPostalCode"] = "کد پستی",
            ["Shipping.EstimateShippingPopUp.ShippingOption.Name"] = "روش ارسال",
            ["Shipping.EstimateShippingPopUp.ShippingOption.EstimatedDelivery"] = "زمان تحویل",
            ["Shipping.EstimateShippingPopUp.ShippingOption.Price"] = "هزینه",
            ["Shipping.EstimateShippingPopUp.SelectShippingOption.Button"] = "اعمال روش ارسال",
            ["Shipping.EstimateShippingPopUp.NoShippingOptions"] = "روشی برای ارسال به این مقصد پیدا نشد.",
            ["Shipping.EstimateShipping.Country.Required"] = "کشور را انتخاب کن.",
            ["Shipping.EstimateShipping.ZipPostalCode.Required"] = "کد پستی را بنویس.",
            ["Shipping.EstimateShipping.City.Required"] = "شهر را بنویس.",
            ["Address.SelectCountry"] = "انتخاب کشور",
            ["Address.Other"] = "سایر",
            ["Media.Product.ImageAlternateTextFormat"] = "تصویر {0}",
            ["Media.Product.ImageAlternateTextFormat.Details"] = "تصویر {0}",
            ["Media.Product.ImageLinkTitleFormat"] = "مشاهدهٔ {0}",
            ["Media.Product.ImageLinkTitleFormat.Details"] = "تصویر {0}",
            ["Common.FileUploader.DropFiles"] = "فایل را اینجا رها کن",
            ["Common.FileUploader.Browse"] = "انتخاب فایل",
            ["Common.FileUploader.Processing"] = "در حال بارگذاری"
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