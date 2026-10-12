using System.Net;
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
            Assert.That(html, Does.Not.Contain("باز کردن سبد خرید"), "The disabled cart capability must hide its action.");
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
                TierPrices = [new(5, "SECRET_TIER")]
            }
        });
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("SECRET_"));
            if (callForPrice)
                Assert.That(html, Does.Contain("CONTACT_FOR_PRICE"));
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
            Assert.That(html, Does.Not.Contain("aria-label=\"افزایش تعداد Product\""), "Allowed native quantities use the selector instead of free increments.");
        });
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
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
            return WebUtility.HtmlDecode(result.ToHtmlString());
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
