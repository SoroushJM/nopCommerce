using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeCatalogAdministrationTests
{
    [Test]
    public async Task MoreThanTwoHundredProductsRemainPagedSearchableAndMultiCategory()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var prefix = "tunitcatalog" + Guid.NewGuid().ToString("N");
        var categoryIds = new List<int>();
        var productIds = new List<int>();
        try
        {
            for (var number = 1; number <= 2; number++)
            {
                using var created = await admin.PostFormAsync("Admin/Category/Create", new()
                {
                    ["Name"] = prefix + " category " + number,
                    ["SeName"] = prefix + "-category-" + number,
                    ["CategoryTemplateId"] = "1",
                    ["Published"] = "true",
                    ["PageSize"] = "12",
                    ["PageSizeOptions"] = "12,24,36",
                    ["AllowCustomersToSelectPageSize"] = "true",
                    ["PriceRangeFiltering"] = "true",
                    ["save-continue"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                });
                categoryIds.Add(await CreatedIdAsync(created));
            }
            for (var number = 1; number <= 201; number++)
            {
                var fields = new Dictionary<string, string>
                {
                    ["Name"] = prefix + " product " + number,
                    ["Sku"] = prefix + "-" + number,
                    ["SeName"] = prefix + "-product-" + number,
                    ["ProductTypeId"] = "5",
                    ["ProductTemplateId"] = "1",
                    ["VisibleIndividually"] = "true",
                    ["Published"] = "true",
                    ["Price"] = (1000 + number).ToString(),
                    ["OrderMinimumQuantity"] = "1",
                    ["OrderMaximumQuantity"] = "10000",
                    ["DisplayOrder"] = number.ToString(),
                    ["SelectedCategoryIds[0]"] = categoryIds[0].ToString(),
                    ["save-continue"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                };
                if (number == 201)
                    fields["SelectedCategoryIds[1]"] = categoryIds[1].ToString();
                using var created = await admin.PostFormAsync("Admin/Product/Create", fields);
                productIds.Add(await CreatedIdAsync(created));
                if (number % 25 == 0)
                    await admin.InitializeAsync("Admin/Product/List");
            }

            var first = await guest.InitializeAsync($"catalog/all?q={prefix}&pagesize=12&orderby=10");
            await Assert.That(NativeCatalogTests.Total(first)).IsEqualTo(201);
            await Assert.That(NativeCatalogTests.ProductIds(first).Count).IsLessThan(201);
            await Assert.That(NativeCatalogTests.ProductIds(first).Contains(productIds[^1])).IsFalse();
            var pager = Regex.Matches(first, "<a[^>]+href=\"([^\"]*pagenumber=([0-9]+)[^\"]*)\"");
            await Assert.That(pager.Count).IsGreaterThan(0);
            var lastPage = pager.Cast<Match>().OrderByDescending(match => int.Parse(match.Groups[2].Value)).First();
            var last = await guest.InitializeAsync(WebUtility.HtmlDecode(lastPage.Groups[1].Value));
            await Assert.That(NativeCatalogTests.Total(last)).IsEqualTo(201);
            await Assert.That(NativeCatalogTests.ProductIds(last).Contains(productIds[^1])).IsTrue();

            var reversed = await guest.InitializeAsync($"catalog/all?q={prefix}&orderby=11");
            await Assert.That(NativeCatalogTests.ProductIds(reversed).First()).IsEqualTo(productIds[^1]);
            var found = await guest.InitializeAsync($"catalog/search?q={prefix}-201");
            await Assert.That(NativeCatalogTests.Total(found)).IsEqualTo(1);
            await Assert.That(NativeCatalogTests.ProductIds(found).Single()).IsEqualTo(productIds[^1]);
            using var product = await guest.GetAsync(prefix + "-product-201");
            await Assert.That(product.StatusCode).IsEqualTo(HttpStatusCode.OK);

            var combined = await guest.InitializeAsync($"catalog/all?q={prefix}&cid={categoryIds[1]}&price=1100-1201");
            await Assert.That(NativeCatalogTests.Total(combined)).IsEqualTo(1);
            await Assert.That(NativeCatalogTests.ProductIds(combined).Single()).IsEqualTo(productIds[^1]);
            var nativeCategory = await guest.InitializeAsync(prefix + "-category-2");
            await Assert.That(NativeCatalogTests.Total(nativeCategory)).IsEqualTo(1);
            await Assert.That(NativeCatalogTests.ProductIds(nativeCategory).Single()).IsEqualTo(productIds[^1]);
            var nativeSearch = await guest.InitializeAsync($"catalog/search?q={prefix}&advs=true&cid={categoryIds[1]}&price=1100-1201");
            await Assert.That(NativeCatalogTests.Total(nativeSearch)).IsEqualTo(NativeCatalogTests.Total(combined));
            await Assert.That(NativeCatalogTests.ProductIds(nativeSearch)).IsEquivalentTo(NativeCatalogTests.ProductIds(combined));
        }
        finally
        {
            await using var cleanup = new StorefrontClient();
            await LocalAdminFixture.SignInAsync(cleanup);
            if (productIds.Count > 0)
            {
                var fields = productIds.Select((id, index) => new KeyValuePair<string, string>($"selectedIds[{index}]", id.ToString())).ToDictionary();
                fields["__RequestVerificationToken"] = cleanup.Token;
                using var deleted = await cleanup.PostFormAsync("Admin/Product/DeleteSelected", fields);
                await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.OK);
            }
            foreach (var id in categoryIds)
            {
                using var deleted = await cleanup.PostFormAsync($"Admin/Category/Delete/{id}", new()
                {
                    ["__RequestVerificationToken"] = cleanup.Token
                });
                await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            }
        }
        var cleaned = await guest.InitializeAsync($"catalog/all?q={prefix}");
        await Assert.That(NativeCatalogTests.Total(cleaned)).IsEqualTo(0);
    }

    private static async Task<int> CreatedIdAsync(HttpResponseMessage response)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var match = Regex.Match(response.Headers.Location?.OriginalString ?? "", "/Edit/([0-9]+)", RegexOptions.IgnoreCase);
        await Assert.That(match.Success).IsTrue();
        return int.Parse(match.Groups[1].Value);
    }
}