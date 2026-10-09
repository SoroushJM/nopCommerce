using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[NotInParallel("storefront-http")]
public sealed class NativeCatalogTests
{
    [Test]
    public async Task GlobalCatalogFiltersAndNativeSearchUseTheSameProductDestinations()
    {
        await using var client = new StorefrontClient();
        var all = await client.InitializeAsync("catalog/all");
        await Assert.That(Total(all)).IsGreaterThan(0);
        await Assert.That(all.Contains("blazor.webassembly.js", StringComparison.Ordinal)).IsTrue();
        await Assert.That(all.Contains("public.catalogproducts.js", StringComparison.Ordinal)).IsFalse();
        await Assert.That(all.Contains("فقط کالاهای موجود", StringComparison.Ordinal)).IsFalse();
        var global = await client.InitializeAsync("catalog/all?q=MRG-1");
        var search = await client.InitializeAsync("catalog/search?q=MRG-1");
        await Assert.That(Total(global)).IsEqualTo(1);
        await Assert.That(Total(search)).IsEqualTo(1);
        await Assert.That(ProductIds(global)).IsEquivalentTo(ProductIds(search));
        var link = Regex.Match(global, "<article[^>]*data-productid=\"[0-9]+\"[^>]*>\\s*<a href=\"([^\"]+)\"");
        await Assert.That(link.Success).IsTrue();
        using var product = await client.GetAsync(WebUtility.HtmlDecode(link.Groups[1].Value));
        await Assert.That(product.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var priced = await client.InitializeAsync("catalog/all?price=0-1");
        await Assert.That(Total(priced)).IsEqualTo(0);
        await Assert.That(ProductIds(priced).Count).IsEqualTo(0);
        var singleGlobal = await client.InitializeAsync("catalog/all?q=MRG-1&price=0-1");
        var singleSearch = await client.InitializeAsync("catalog/search?q=MRG-1&price=0-1");
        await Assert.That(Total(singleGlobal)).IsEqualTo(1);
        await Assert.That(ProductIds(singleGlobal)).IsEquivalentTo(ProductIds(singleSearch));
    }

    [Test]
    public async Task InvalidCategoryIsRejectedAndInvalidSelectorsUseConfiguredDefaults()
    {
        await using var client = new StorefrontClient();
        using var invalidCategory = await client.GetAsync("catalog/all?cid=2147483647");
        await Assert.That(invalidCategory.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        var normal = await client.InitializeAsync("catalog/all");
        var invalid = await client.InitializeAsync("catalog/all?orderby=999&pagesize=-1&pagenumber=-1&viewmode=bogus");
        await Assert.That(Total(invalid)).IsEqualTo(Total(normal));
        await Assert.That(ProductIds(invalid)).IsEquivalentTo(ProductIds(normal));
        await Assert.That(invalid.Contains("data-catalog-page=\"1\"", StringComparison.Ordinal)).IsTrue();
    }

    internal static int Total(string html)
    {
        var match = Regex.Match(html, "data-catalog-total=\"([0-9]+)\"");
        if (!match.Success)
            throw new InvalidOperationException("Native catalog totals were not rendered.");
        return int.Parse(match.Groups[1].Value);
    }

    [Test]
    public async Task MainRoutesAndLegacyCatalogLinksReachNativePages()
    {
        await using var client = new StorefrontClient();
        var home = await client.InitializeAsync("");
        await Assert.That(home.Contains("\"type\":\"server\"", StringComparison.Ordinal)).IsFalse();
        await Assert.That(ProductIds(home).Count).IsGreaterThanOrEqualTo(8);
        var search = await client.InitializeAsync("search?q=MRG-1");
        await Assert.That(Total(search)).IsEqualTo(1);
        var preview = await client.InitializeAsync("catalog/search?q=MRG-1");
        await Assert.That(ProductIds(search)).IsEquivalentTo(ProductIds(preview));
        using var legacy = await client.GetAsync("stationery/catalog?sort=price&q=MRG-1&viewmode=list&pagesize=3");
        await Assert.That(legacy.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var target = legacy.Headers.Location!.OriginalString;
        await Assert.That(target.StartsWith("/catalog/all?", StringComparison.Ordinal)).IsTrue();
        foreach (var query in new[] { "orderby=10", "q=MRG-1", "viewmode=list", "pagesize=3" })
            await Assert.That(target.Contains(query, StringComparison.Ordinal)).IsTrue();
        await Assert.That(Total(await client.InitializeAsync(target))).IsEqualTo(1);
        using var category = await client.GetAsync("stationery/catalog?category=" + Uri.EscapeDataString("دفتر و کاغذ"));
        await Assert.That(category.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(category.Headers.Location!.OriginalString.Contains("catalog/all", StringComparison.Ordinal)).IsFalse();
        await Assert.That(Total(await client.InitializeAsync(category.Headers.Location.OriginalString))).IsGreaterThan(0);
        using var unavailable = await client.GetAsync("stationery/catalog?category=missing-tunit-category");
        await Assert.That(unavailable.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    internal static List<int> ProductIds(string html)
    {
        return Regex.Matches(html, "data-productid=\"([0-9]+)\"").Select(match => int.Parse(match.Groups[1].Value)).ToList();
    }
}