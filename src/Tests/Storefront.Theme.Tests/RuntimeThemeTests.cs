using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Storefront.Theme.Tests;

[TestFixture, NonParallelizable, Category("Runtime")]
public sealed class RuntimeThemeTests
{
    [TestCase("MADADRANG", "Madadrang", "storefront.css")]
    [TestCase("MISHASHOP", "MishaShop", "mishashop.css")]
    public async Task ThemeServesItsOwnShellNativeHeaderFooterAndRealAssets(string key, string theme, string css)
    {
        using var client = Client(key);
        var home = await Html(client, "");
        var expectedCss = $"_content/Storefront.UI.{theme}/{css}";
        Assert.Multiple(() =>
        {
            Assert.That(Regex.Matches(home, "<!DOCTYPE html>", RegexOptions.IgnoreCase), Has.Count.EqualTo(1));
            Assert.That(home, Does.Not.Contain("_content/Storefront.UI/storefront.css"));
            Assert.That(home, Does.Not.Contain(theme == "MishaShop" ? "_content/Storefront.UI.Madadrang/storefront.css" : "_content/Storefront.UI.MishaShop/mishashop.css"));
            Assert.That(home, Does.Contain("header-links"));
            Assert.That(home, Does.Contain("small-searchterms"), "The native search/autocomplete box must remain present.");
            Assert.That(home, Does.Contain("AjaxCart.init"));
            Assert.That(home, Does.Contain("footer-disclaimer"));
            Assert.That(home, Does.Contain(theme == "MishaShop" ? "mishashop-brand" : "sf-brand"));
        });
        foreach (var (path, mediaType) in new[]
        {
            (expectedCss, "text/css"),
            ("_content/Storefront.UI.Shared/storefront.js", "javascript"),
            ("_framework/blazor.webassembly.js", "javascript")
        })
        {
            using var response = await client.GetAsync(path);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Does.Contain(mediaType), path);
            Assert.That(await response.Content.ReadAsStringAsync(), Is.Not.Empty, path);
        }
        var linkedStyles = Regex.Matches(home, "<link\\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(match => match.Value).Where(tag => Attribute(tag, "rel") == "stylesheet")
            .Select(tag => new Uri(client.BaseAddress!, Attribute(tag, "href"))).ToArray();
        Assert.That(linkedStyles, Is.Not.Empty, "The rendered page must load a stylesheet, including when native bundling is enabled.");
        var stylesheetContents = new List<string>();
        foreach (var uri in linkedStyles.Where(uri => uri.Authority == client.BaseAddress!.Authority))
        {
            using var response = await client.GetAsync(uri);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), uri.AbsolutePath);
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/css"), uri.AbsolutePath);
            stylesheetContents.Add(await response.Content.ReadAsStringAsync());
        }
        var renderedCss = string.Join("\n", stylesheetContents);
        Assert.That(renderedCss, Does.Contain(theme == "MishaShop" ? ".mishashop-brand" : ".brand-word"),
            "The CSS actually linked by the page must contain the current theme's branding styles.");
        Assert.That(renderedCss, Does.Not.Contain(theme == "MishaShop" ? ".brand-word" : ".mishashop-brand"),
            "The rendered stylesheet must not carry the other theme's branding styles.");
    }

    [TestCase("MADADRANG")]
    [TestCase("MISHASHOP")]
    public async Task SearchAndAccountRoutesRetainNativeSelectableControls(string key)
    {
        using var client = Client(key);
        var search = await Html(client, "search?advs=true&sid=true&sit=true");
        foreach (var field in new[] { "q", "advs", "sid", "sit" })
        {
            var controls = Controls(search).Where(item => Attribute(item, "name") == field).ToArray();
            Assert.That(controls.Any(item => Attribute(item, "type") != "hidden"), Is.True, $"Native search field {field} must be selectable, not hidden.");
        }
        foreach (var field in new[] { "sid", "sit" })
            Assert.That(Controls(search).Any(item => Attribute(item, "name") == field && Regex.IsMatch(item, "\\bchecked(?:\\s|=|>)")), Is.True, "Native query state must be restored.");
        var login = await Html(client, "login");
        Assert.That(Controls(login).Any(item => Attribute(item, "name") == "Password"), Is.True);
        Assert.That(Token(login), Is.Not.Empty);
        var registration = await Html(client, "register");
        Assert.That(registration, Does.Contain("registration-page"));
        foreach (var path in new[] { "wishlist", "compareproducts", "cart" })
            Assert.That(await Html(client, path), Does.Contain("<!DOCTYPE html>"), path);
    }

    [TestCase("MADADRANG")]
    [TestCase("MISHASHOP")]
    public async Task ProductRendererPreservesNativeFormsSelectionAndSupplementalActions(string key)
    {
        using var client = Client(key);
        var productId = ProductId(key);
        var html = await Html(client, $"product/productdetails?productId={productId}");
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Not.Contain("\"type\":\"server\""));
            Assert.That(html, Does.Contain("product-details-form"));
            Assert.That(Controls(html).Any(item => Attribute(item, "name") == $"addtocart_{productId}.EnteredQuantity"), Is.True);
            Assert.That(Token(html), Is.Not.Empty);
        });
        if (key == "MADADRANG")
        {
            Assert.That(html, Does.Contain("\"type\":\"webassembly\""), "Madadrang's supported product controls prerender their interactive Shared renderer.");
            Assert.That(html, Does.Contain("sf-product-name"));
        }
        else
        {
            Assert.That(html, Does.Contain("class=\"product-name\""), "MishaShop preserves the full native product renderer.");
            Assert.That(html, Does.Contain($"add-to-cart-button-{productId}"));
            Assert.That(html, Does.Contain("<details class=\"mishashop-accordion\""), "The configured sample product includes native details/review content in accessible accordions.");
            using var response = await client.GetAsync($"product/combinations?productId={productId}");
            response.EnsureSuccessStatusCode();
            using var combinations = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var attribute in combinations.RootElement.EnumerateArray()
                .SelectMany(item => item.GetProperty("Attributes").EnumerateArray()).Select(item => item.GetProperty("Id").GetInt32()).Distinct())
                Assert.That(Controls(html).Any(item => Attribute(item, "name") == $"product_attribute_{attribute}"), Is.True,
                    $"Native attribute {attribute} must retain an actual form control on MishaShop.");
        }
        var home = await Html(client, "");
        if (home.Contains("ico-wishlist", StringComparison.Ordinal))
            Assert.That(html, Does.Contain("add-to-wishlist-button"), "A wishlist-enabled fixture must preserve the native product action.");
        var expectations = QaEnvironment(key, "PRODUCT_MARKERS");
        if (!string.IsNullOrWhiteSpace(expectations))
            foreach (var marker in JsonSerializer.Deserialize<string[]>(expectations)!)
                Assert.That(html, Does.Contain(marker), "Configured native product fixture marker");
    }

    [TestCase("MADADRANG")]
    [TestCase("MISHASHOP")]
    public async Task ConfiguredUnsupportedProductsKeepTheirNativeControls(string key)
    {
        using var client = Client(key);
        var fixtures = QaEnvironment(key, "FALLBACKS");
        if (string.IsNullOrWhiteSpace(fixtures))
            Assert.Ignore("Supply isolated fixture fallback expectations as JSON [{\"productId\":2,\"markers\":[\"rental\"]}].");
        using var json = JsonDocument.Parse(fixtures!);
        foreach (var fixture in json.RootElement.EnumerateArray())
        {
            var id = fixture.GetProperty("productId").GetInt32();
            var html = await Html(client, $"product/productdetails?productId={id}");
            if (key == "MADADRANG")
                Assert.That(html, Does.Not.Contain("sf-product-name"), "Unsupported Madadrang products must leave the Shared product body.");
            else
                Assert.That(html, Does.Contain("class=\"product-name\""), "MishaShop retains native rendering for all attribute types.");
            Assert.That(html, Does.Contain("product-details-form"));
            Assert.That(Token(html), Is.Not.Empty);
            foreach (var marker in fixture.GetProperty("markers").EnumerateArray())
                Assert.That(html, Does.Contain(marker.GetString()), $"Fallback product {id}");
        }
    }

    [TestCase("MADADRANG")]
    [TestCase("MISHASHOP")]
    [Category("IsolatedCart")]
    public async Task GuestNativeCartRequiresCsrfOwnsItemsAndSynchronizesSections(string key)
    {
        using var owner = Client(key);
        using var other = Client(key);
        if (Environment.GetEnvironmentVariable("STOREFRONT_QA_ISOLATED") != "1")
            Assert.Ignore("Cart mutations require STOREFRONT_QA_ISOLATED=1 and an explicitly supplied loopback QA URL.");
        var productId = ProductId(key);
        var product = await Html(owner, $"product/productdetails?productId={productId}");
        var token = Token(product);
        var fields = new Dictionary<string, string> { [$"addtocart_{productId}.EnteredQuantity"] = "1" };
        foreach (var input in Controls(product))
        {
            var name = Attribute(input, "name");
            if (name.StartsWith("product_attribute_", StringComparison.Ordinal) && Attribute(input, "type") == "hidden")
                fields[name] = Attribute(input, "value");
        }
        using (var response = await owner.GetAsync($"product/combinations?productId={productId}"))
        {
            response.EnsureSuccessStatusCode();
            using var combinations = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var available = combinations.RootElement.EnumerateArray().FirstOrDefault(item => item.GetProperty("InStock").GetBoolean());
            if (available.ValueKind != JsonValueKind.Undefined)
                foreach (var attribute in available.GetProperty("Attributes").EnumerateArray())
                    fields[$"product_attribute_{attribute.GetProperty("Id").GetInt32()}"] = attribute.GetProperty("ValueIds")[0].GetInt32().ToString();
        }
        var addPath = $"addproducttocart/details/{productId}/1";
        using (var missing = await owner.PostAsync(addPath, new FormUrlEncodedContent(fields)))
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        fields["__RequestVerificationToken"] = token;
        try
        {
            using (var added = await owner.PostAsync(addPath, new FormUrlEncodedContent(fields)))
            {
                added.EnsureSuccessStatusCode();
                using var result = JsonDocument.Parse(await added.Content.ReadAsStringAsync());
                Assert.That(result.RootElement.GetProperty("success").GetBoolean(), Is.True, result.RootElement.ToString());
            }
            var cart = await Cart(owner);
            Assert.That(cart.GetProperty("Count").GetInt32(), Is.EqualTo(1));
            Assert.That(cart.GetProperty("SignedIn").GetBoolean(), Is.False);
            var lineId = cart.GetProperty("Lines")[0].GetProperty("Id").GetInt32();
            CheckSections(cart);
            var foreignCart = await Cart(other);
            _ = await Update(other, foreignCart, lineId, 3);
            Assert.That((await Cart(owner)).GetProperty("Lines")[0].GetProperty("Quantity").GetInt32(), Is.EqualTo(1), "A different guest must not mutate the owner's line.");
            var updated = await Update(owner, cart, lineId, 2);
            Assert.That(updated.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32(), Is.EqualTo(2));
            CheckSections(updated);
            var fullCart = await Html(owner, "cart");
            Assert.That(fullCart, Does.Contain($"itemquantity{lineId}"));
            Assert.That(fullCart, Does.Contain("total-info"));
            var removed = await Update(owner, updated, lineId, 0);
            Assert.That(removed.GetProperty("Count").GetInt32(), Is.EqualTo(0));
            CheckSections(removed);
        }
        finally
        {
            var cart = await Cart(owner);
            foreach (var line in cart.GetProperty("Lines").EnumerateArray())
                cart = await Update(owner, cart, line.GetProperty("Id").GetInt32(), 0);
            Assert.That(cart.GetProperty("Count").GetInt32(), Is.EqualTo(0), "Only this new guest's cart must be cleaned up.");
        }
    }

    private static string? QaEnvironment(string key, string suffix) =>
        Environment.GetEnvironmentVariable($"STOREFRONT_QA_{key}_{suffix}")
        ?? (key == "MISHASHOP" ? Environment.GetEnvironmentVariable($"STOREFRONT_QA_BOUTIQUE_{suffix}") : null);

    private static HttpClient Client(string key)
    {
        var configured = QaEnvironment(key, "URL");
        if (string.IsNullOrWhiteSpace(configured)) Assert.Ignore($"Set STOREFRONT_QA_{key}_URL to the separate local QA host.");
        var uri = new Uri(configured!, UriKind.Absolute);
        if (!uri.IsLoopback || uri.Scheme != "http" || uri.UserInfo.Length > 0 || uri.Port == 5090)
            throw new InvalidOperationException("Runtime verification requires an explicitly supplied HTTP loopback QA host other than the active 5090 installation.");
        return new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = true })
        { BaseAddress = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
    }

    private static int ProductId(string key)
    {
        var value = QaEnvironment(key, "PRODUCT_ID");
        if (!int.TryParse(value, out var id) || id <= 0) Assert.Ignore($"Supply STOREFRONT_QA_{key}_PRODUCT_ID for a published supported product fixture.");
        return id;
    }

    private static async Task<string> Html(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), path);
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"), path);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static IEnumerable<string> Controls(string html) => Regex.Matches(html, "<(?:input|select|textarea)\\b[^>]*>", RegexOptions.IgnoreCase).Select(match => match.Value);
    private static string Attribute(string tag, string name) => WebUtility.HtmlDecode(Regex.Match(tag, $"\\b{Regex.Escape(name)}=\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value);
    private static string Token(string html) => Controls(html).Where(item => Attribute(item, "name") == "__RequestVerificationToken").Select(item => Attribute(item, "value")).FirstOrDefault() ?? "";

    private static async Task<JsonElement> Cart(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "cart");
        request.Headers.Add("X-Storefront-Cart", "1");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static async Task<JsonElement> Update(HttpClient client, JsonElement cart, int lineId, int quantity)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("updatecart", "1"), new(cart.GetProperty("TokenField").GetString()!, cart.GetProperty("Token").GetString()!)
        };
        foreach (var field in cart.GetProperty("CheckoutFields").EnumerateArray())
            foreach (var value in field.GetProperty("Values").EnumerateArray())
                fields.Add(new(field.GetProperty("Name").GetString()!, value.GetString()!));
        fields.Add(quantity == 0 ? new("removefromcart", lineId.ToString()) : new($"itemquantity{lineId}", quantity.ToString()));
        using var request = new HttpRequestMessage(HttpMethod.Post, "cart") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("X-Storefront-Cart", "1");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.Clone();
    }

    private static void CheckSections(JsonElement cart)
    {
        var sections = cart.GetProperty("NativeSections");
        Assert.That(sections.GetProperty("updatetopcartsectionhtml").GetString(), Is.Not.Empty);
        if (cart.GetProperty("MiniEnabled").GetBoolean())
            Assert.That(sections.GetProperty("updateflyoutcartsectionhtml").GetString(), Does.Contain("flyout-cart"));
    }
}
