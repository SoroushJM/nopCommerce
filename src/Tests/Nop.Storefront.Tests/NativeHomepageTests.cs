using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[NotInParallel("storefront-http")]
public sealed class NativeHomepageTests
{
    [Test]
    public async Task HomepageUsesInitialHtmlAndRealProductDestinations()
    {
        await using var client = new StorefrontClient();
        var html = await client.InitializeAsync("home/index");
        await Assert.That(Regex.Matches(html, "<!DOCTYPE html>", RegexOptions.IgnoreCase).Count).IsEqualTo(1);
        await Assert.That(html.Contains("\"type\":\"server\"", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("/_framework/blazor.webassembly.js", StringComparison.Ordinal)).IsTrue();

        var cards = Regex.Matches(html, "data-productid=\"[0-9]+\"[^>]*>\\s*<a href=\"([^\"]+)\"");
        await Assert.That(cards.Count).IsGreaterThanOrEqualTo(8);
        foreach (Match card in cards)
        {
            var url = WebUtility.HtmlDecode(card.Groups[1].Value);
            await Assert.That(!url.Contains("stationery/product", StringComparison.Ordinal)).IsTrue();
            using var response = await client.GetAsync(url);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        var prices = Regex.Matches(WebUtility.HtmlDecode(html), "<p class=\"text-sm font-bold price\">([^<]+)</p>");
        await Assert.That(prices.Count).IsEqualTo(cards.Count);
        await Assert.That(prices.All(match => match.Groups[1].Value.Contains("تومان", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task HomepageHasNativeCategoryUrlsAndServedProductPictures()
    {
        await using var client = new StorefrontClient();
        var html = await client.InitializeAsync("home/index");
        var categories = Regex.Matches(html, "<a href=\"([^\"]+)\"[^>]+data-categoryid=\"[0-9]+\"");
        await Assert.That(categories.Count).IsGreaterThanOrEqualTo(4);
        foreach (Match category in categories)
        {
            var url = WebUtility.HtmlDecode(category.Groups[1].Value);
            await Assert.That(!url.Contains("stationery/catalog", StringComparison.Ordinal)).IsTrue();
            using var response = await client.GetAsync(url);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
        var pictures = Regex.Matches(html, "<div class=\"product-photo\">\\s*<img src=\"([^\"]+)\"");
        await Assert.That(pictures.Count).IsGreaterThanOrEqualTo(8);
        foreach (var url in pictures.Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).Distinct())
        {
            using var image = await client.GetAsync(url);
            await Assert.That(image.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(image.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.Ordinal) == true).IsTrue();
        }
    }

    [Test]
    public async Task FixtureMaintenanceRejectsAnonymousAccess()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync("home/index");
        const string path = "Admin/PersianStorefrontMaintenance/Configure";
        using var get = await client.GetAsync(path);
        using var post = await client.PostFormAsync(path, new()
        {
            ["__RequestVerificationToken"] = client.Token
        });
        foreach (var response in new[] { get, post })
        {
            await Assert.That(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden).IsTrue();
            if (response.StatusCode == HttpStatusCode.Redirect)
                await Assert.That(response.Headers.Location?.OriginalString.Contains("login", StringComparison.OrdinalIgnoreCase) == true).IsTrue();
        }
    }
}