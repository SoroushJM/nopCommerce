using System.Net;
using System.Xml.Linq;
using BlazorBlueprint.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using NUnit.Framework;
using Storefront.UI.Shared;
using Storefront.UI.Shared.Components;

namespace Storefront.Theme.Tests;

// These render the actual shared components: no nopCommerce database or running host required.
// Native controller/fallback and live browser interactions remain integration-test concerns.
[TestFixture]
public sealed class NativeFeatureRenderingTests
{
    [TestCase(false, "/shop/fa/login")]
    [TestCase(true, "/shop/fa/customer/info")]
    public async Task DisabledOtpUsesProvidedNativeAccountRoutes(bool signedIn, string accountRoute)
    {
        var html = await Render<NativeHeader>(new()
        {
            [nameof(NativeHeader.Theme)] = new StorefrontThemeProfile
            {
                HomeUrl = "/shop/fa/", CatalogUrl = "/shop/fa/catalog/all",
                LoginUrl = "/shop/fa/login", AccountUrl = "/shop/fa/customer/info", EnableOtp = false
            },
            [nameof(NativeHeader.Initial)] = new StoreNavigation { SignedIn = signedIn, CartEnabled = false },
            [nameof(NativeHeader.OpenLoginOnLoad)] = true
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain($"href=\"{accountRoute}\""));
            Assert.That(html, Does.Contain("href=\"/shop/fa/\""));
            Assert.That(html, Does.Contain("href=\"/shop/fa/catalog/all\""));
            Assert.That(html, Does.Not.Contain("sf-phone"), "A native login theme must not expose the mock OTP dialog.");
            Assert.That(html, Does.Not.Contain("aria-label=\"سبد خرید\""), "The disabled cart capability must hide its action.");
        });
    }

    [Test]
    public async Task ProductSelectionRemainsSerializableByNativeWishlistAndCartForms()
    {
        var html = await Render<NativeProductPage>(new()
        {
            [nameof(NativeProductPage.Initial)] = new NativeProduct
            {
                Id = 73, Name = "Product", Quantity = 3, CartItemId = 91,
                BreadcrumbEnabled = true, HomeUrl = "/shop/fa/", InStock = true,
                Attributes =
                [
                    new(10, "Size", 1, true, false, [new(101, "Medium", "", "", true), new(102, "Large", "", "", false)]),
                    new(20, "Color", 40, true, false, [new(201, "Blue", "#0000ff", "", true)])
                ]
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("name=\"product_attribute_10\" value=\"101\""));
            Assert.That(html, Does.Contain("name=\"product_attribute_20\" value=\"201\""));
            Assert.That(html, Does.Contain("name=\"addtocart_73.EnteredQuantity\" value=\"3\""));
            Assert.That(html, Does.Contain("name=\"addtocart_73.UpdatedShoppingCartItemId\" value=\"91\""));
            Assert.That(html, Does.Contain("href=\"/shop/fa/\""));
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task RestrictedPricesNeverLeakThroughAlternativePriceDisplays(bool hidePrices, bool callForPrice)
    {
        var html = await Render<NativeProductPage>(new()
        {
            [nameof(NativeProductPage.Initial)] = new NativeProduct
            {
                Id = 73, Name = "Product", HidePrices = hidePrices, CallForPrice = callForPrice,
                CallForPriceLabel = "CONTACT_FOR_PRICE", Price = "SECRET_PRICE", OldPrice = "SECRET_OLD",
                ReferencePrice = "SECRET_REFERENCE", BasePrice = "SECRET_BASE", InStock = true,
                TierPrices = [new(5, "SECRET_TIER")],
                Attributes = [new(10, "Size", 2, true, false,
                    [new(101, "Medium", "", "", true) { DisplayName = "Medium (SECRET_OPTION_PRICE)" }])]
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("SECRET_"));
            if (callForPrice)
                Assert.That(html, Does.Contain("CONTACT_FOR_PRICE"));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(40)]
    [TestCase(45)]
    public async Task ProductChoicesKeepNativeInstructionsAndPreparedPriceLabelsWithoutExecutingPlainText(int controlType)
    {
        var html = await Render<NativeProductPage>(new()
        {
            [nameof(NativeProductPage.Initial)] = new NativeProduct
            {
                Id = 73, Name = "Product", InStock = true,
                Attributes =
                [
                    new(10, "Original name", controlType, true, false,
                        [new(101, "<em>Natural</em>", "#aabbcc", "/media/choice.png", true)
                            { DisplayName = "<em>Natural</em> (+15%)" }])
                    {
                        TextPrompt = "<strong>Choose fit</strong>",
                        Description = "<b>Prepared native sizing guide</b>"
                    },
                    new(20, "Fallback prompt", 2, false, false, [new(201, "Plain option", "", "", true)])
                ]
            }
        }, decodeHtml: false);
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("&lt;strong&gt;Choose fit&lt;/strong&gt;"));
            Assert.That(WebUtility.HtmlDecode(html), Does.Contain("<em>Natural</em> (+15%)"));
            Assert.That(html, Does.Contain("&lt;em&gt;Natural&lt;/em&gt;"));
            Assert.That(html, Does.Contain("<b>Prepared native sizing guide</b>"));
            Assert.That(html, Does.Contain("class=\"required\">*</span>"));
            Assert.That(html, Does.Contain("Fallback prompt"));
            Assert.That(html, Does.Contain("Plain option"));
            Assert.That(html, Does.Not.Contain("<strong>Choose fit</strong>"));
            Assert.That(html, Does.Not.Contain("<em>Natural</em>"));
        });
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task CartLineHonorsRemovalAndPictureSettingsAndKeepsNativeDetails(bool showImage, bool disableRemoval)
    {
        var html = await Render<NativeCartItem>(new()
        {
            [nameof(NativeCartItem.ShowImage)] = showImage,
            [nameof(NativeCartItem.Line)] = new NativeCartLine
            {
                Id = 7, Name = "Product", Url = "/product", EditUrl = "/product?updatecartitemid=7",
                Image = "/media/product.png", Quantity = 2, DisableRemoval = disableRemoval,
                AttributeInfo = "<b>ATTRIBUTES</b>", RentalInfo = "RENTAL_DATES", RecurringInfo = "RECURRING_PERIOD",
                SubTotal = "SUBTOTAL", UnitPrice = "UNIT_PRICE", Discount = "DISCOUNT", DiscountNote = "DISCOUNT_LIMIT",
                AllowedQuantities = [new("2", "2", true), new("5", "5")]
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(html.Contains("src=\"/media/product.png\""), Is.EqualTo(showImage));
            Assert.That(html.Contains("aria-label=\"حذف Product\""), Is.EqualTo(!disableRemoval));
            Assert.That(html, Does.Contain("href=\"/product?updatecartitemid=7\""));
            Assert.That(html, Does.Contain("<b>ATTRIBUTES</b>"));
            Assert.That(html, Does.Contain("RENTAL_DATES"));
            Assert.That(html, Does.Contain("RECURRING_PERIOD"));
            Assert.That(html, Does.Contain("DISCOUNT_LIMIT"));
            Assert.That(html, Does.Contain("aria-label=\"تعداد Product\""));
            Assert.That(html, Does.Not.Contain("aria-label=\"تعداد + Product\""), "Allowed native quantities use the selector instead of free increments.");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProductUsesNativePreparedLanguageAndDirectionWhileKeepingFormattedFields(bool rtl)
    {
        var html = await Render<NativeProductPage>(new()
        {
            [nameof(NativeProductPage.Initial)] = new NativeProduct
            {
                Id = 73, Name = "Product", Labels = NativeEnglishLabels(rtl), InStock = true,
                BreadcrumbEnabled = true, ShowSku = true, Sku = "SKU123", ShowMpn = true, Mpn = "MPN123",
                ShowGtin = true, Gtin = "GTIN123", ShowVendor = true, Vendor = new(1, "Vendor", "/vendor"),
                IsShipEnabled = true, IsFreeShipping = true, FreeShippingNotificationEnabled = true, DeliveryDate = "SERVER_DELIVERY",
                PurchaseLabel = "SERVER_PURCHASE_LABEL", MinimumQuantityNotification = "SERVER_MINIMUM",
                CallForPriceLabel = "SERVER_CALL_LABEL", Price = "SERVER_PRICE", TierPrices = [new(5, "SERVER_TIER")],
                Attributes = [new(10, "Size", 1, true, false, [new(101, "Medium", "", "", true)])]
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain($"data-bb-direction=\"{(rtl ? "rtl" : "ltr")}\""));
            Assert.That(html, Does.Contain($"class=\"storefront\" dir=\"{(rtl ? "rtl" : "ltr")}\""));
            foreach (var text in new[] { "SKU", "Manufacturer part number", "GTIN", "Free shipping", "Delivery date", "Enter a quantity", "PRICE BREAKS", "SERVER_PURCHASE_LABEL", "SERVER_MINIMUM", "SERVER_DELIVERY", "SERVER_PRICE", "SERVER_TIER" })
                Assert.That(html, Does.Contain(text));
            Assert.That(html, Does.Not.Contain("قیمت خرید تعدادی"));
            Assert.That(html, Does.Not.Contain("کد تولیدکننده"));
        });
    }

    [Test]
    public async Task NativeHeaderAndCartLineUsePreparedNavigationAndActionTextWithoutRenderingTextAsHtml()
    {
        var labels = NativeEnglishLabels(false);
        labels.Values["Account.Login"] = "Log in <em>native</em>";
        var header = await Render<NativeHeader>(new()
        {
            [nameof(NativeHeader.Theme)] = new StorefrontThemeProfile { Labels = labels, EnableOtp = false },
            [nameof(NativeHeader.Initial)] = new StoreNavigation { CartEnabled = true, CartCount = 2 }
        }, decodeHtml: false);
        var line = await Render<NativeCartItem>(new()
        {
            [nameof(NativeCartItem.Labels)] = labels,
            [nameof(NativeCartItem.Line)] = new NativeCartLine
            {
                Id = 7, Name = "Product", Quantity = 2, EditUrl = "/product?updatecartitemid=7",
                UnitPrice = "SERVER_UNIT_PRICE", SubTotal = "SERVER_TOTAL", Discount = "SERVER_DISCOUNT"
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(header, Does.Contain("data-bb-direction=\"ltr\""));
            Assert.That(header, Does.Contain("Log in &lt;em&gt;native&lt;/em&gt;"));
            Assert.That(header, Does.Not.Contain("<em>native</em>"));
            Assert.That(header, Does.Contain("aria-label=\"Shopping cart\""));
            Assert.That(header, Does.Contain("My account"));
            Assert.That(header, Does.Not.Contain("حساب من"));
            Assert.That(line, Does.Contain("dir=\"ltr\""));
            Assert.That(line, Does.Contain("aria-label=\"Remove Product\""));
            Assert.That(line, Does.Contain("aria-label=\"Qty + Product\""));
            Assert.That(line, Does.Contain("Edit"));
            Assert.That(line, Does.Contain("Unit price: SERVER_UNIT_PRICE"));
            Assert.That(line, Does.Contain("Discount: SERVER_DISCOUNT"));
        });
    }

    [Test]
    public void EveryPreparedLabelHasAnExistingNativeResourceAndDefaultRemainsPersianRtl()
    {
        var labels = NativeEnglishLabels(false);
        Assert.Multiple(() =>
        {
            foreach (var resource in NativeUiLabels.Defaults.Keys)
                Assert.That(labels.Values.ContainsKey(resource), Is.True, $"Native resource missing: {resource}");
            var defaults = new NativeUiLabels();
            Assert.That(defaults.Direction, Is.EqualTo("rtl"));
            Assert.That(defaults.Get("Homepage"), Is.EqualTo("خانه"));
        });
    }

    private static NativeUiLabels NativeEnglishLabels(bool rtl) => new()
    {
        Rtl = rtl,
        Values = XDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "native-resources.xml"))
            .Descendants("LocaleResource").ToDictionary(resource => (string)resource.Attribute("Name")!,
                resource => resource.Element("Value")!.Value)
    };

    [TestCase(false)]
    [TestCase(true)]
    public async Task CatalogMenuAndNativeSelectFollowPreparedDirectionWithoutLosingNativeValues(bool rtl)
    {
        var labels = NativeEnglishLabels(rtl);
        var toolbar = await Render<NativeCatalogControls>(new()
        {
            [nameof(NativeCatalogControls.Placement)] = "toolbar",
            [nameof(NativeCatalogControls.Initial)] = new CatalogControls
            {
                Rtl = rtl, Labels = labels,
                SortOptions = [new("10", "Price low to high", true)],
                PageSizes = [new("12", "12", true)], ViewModes = [new("grid", "Grid", true)]
            }
        });
        var filters = await Render<NativeCatalogControls>(new()
        {
            [nameof(NativeCatalogControls.Initial)] = new CatalogControls
            {
                Rtl = rtl, Labels = labels,
                Categories = [new("7", "Shirts", true)], Manufacturers = [new("8", "Brand", true)],
                PriceEnabled = true, PriceFrom = "10", PriceTo = "20", Currency = "USD"
            }
        });
        var form = await Render<NativeFormSelect>(new()
        {
            [nameof(NativeFormSelect.Initial)] = new NativeFormSelectInput
            {
                Rtl = rtl, Name = "product_attribute_73", Label = "Native size prompt",
                Options = [new("101", "Medium", true)]
            }
        });
        var menu = await Render<StorefrontMainMenu>(new()
        {
            [nameof(StorefrontMainMenu.Menu)] = new StoreMenu
            {
                Rtl = rtl, Labels = labels, Id = 1, Name = "Main",
                Items = [new() { Id = 1, Title = "Native shirts", Url = "/shirts" }]
            }
        });
        Assert.Multiple(() =>
        {
            foreach (var html in new[] { toolbar, filters, form, menu })
                Assert.That(html, Does.Contain($"data-bb-direction=\"{(rtl ? "rtl" : "ltr")}\""));
            Assert.That(toolbar, Does.Contain("Select product sort order"));
            Assert.That(toolbar, Does.Contain("Price low to high"));
            Assert.That(filters, Does.Contain("Category"));
            Assert.That(filters, Does.Contain("Filter by price"));
            Assert.That(filters, Does.Not.Contain("بازهٔ قیمت"));
            Assert.That(form, Does.Contain("name=\"product_attribute_73\" id=\"product_attribute_73\" value=\"101\""));
            Assert.That(form, Does.Contain("Native size prompt"));
            Assert.That(menu, Does.Contain("Native shirts"));
            Assert.That(menu, Does.Contain("href=\"/shirts\""));
            Assert.That(menu, Does.Contain("aria-label=\"Categories\""));
        });
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters, bool decodeHtml = true) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBlazorBlueprintComponents();
        services.AddSingleton<IJSRuntime, NoJavaScript>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            var html = result.ToHtmlString();
            return decodeHtml ? WebUtility.HtmlDecode(html) : html;
        });
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("https://example.test/shop/", "https://example.test/shop/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static rendering must not invoke JavaScript.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
