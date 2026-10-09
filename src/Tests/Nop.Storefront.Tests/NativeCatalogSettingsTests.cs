using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeCatalogSettingsTests
{
    [Test]
    public async Task DisablingNativeFacetsHidesControlsAndGlobalCatalogIgnoresTheirQueryValues()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        await admin.InitializeAsync("Admin/Setting/AllSettings");
        var originals = new List<JsonElement>();
        foreach (var name in new[] { "catalogsettings.enablemanufacturerfiltering", "catalogsettings.enablespecificationattributefiltering", "catalogsettings.enablepricerangefiltering" })
        {
            using var response = await admin.PostFormAsync("Admin/Setting/AllSettings", new()
            {
                ["SearchSettingName"] = name,
                ["Length"] = "100",
                ["__RequestVerificationToken"] = admin.Token
            });
            var rows = (await StorefrontClient.ReadJsonAsync(response)).GetProperty("Data").EnumerateArray()
                .Where(row => row.GetProperty("Name").GetString() == name).ToArray();
            await Assert.That(rows.Length).IsEqualTo(1);
            await Assert.That(rows[0].GetProperty("StoreId").GetInt32()).IsEqualTo(0);
            originals.Add(rows[0]);
        }
        var before = await guest.InitializeAsync("catalog/all");
        var home = await guest.InitializeAsync("");
        var categoryUrl = WebUtility.HtmlDecode(Regex.Match(home, "<a href=\"([^\"]+)\"[^>]+data-categoryid=\"[0-9]+\"").Groups[1].Value);
        await Assert.That(categoryUrl.Length).IsGreaterThan(0);
        await Assert.That(before.Contains("sf-catalog-desktop-brand-", StringComparison.Ordinal)).IsTrue();
        await Assert.That(before.Contains("sf-catalog-desktop-spec-", StringComparison.Ordinal)).IsTrue();
        await Assert.That(before.Contains("sf-catalog-desktop-from", StringComparison.Ordinal)).IsTrue();
        try
        {
            foreach (var row in originals)
                await UpdateAsync(admin, row, "False");
            var after = await guest.InitializeAsync("catalog/all?ms=2147483647&specs=2147483647&price=0-1");
            await Assert.That(NativeCatalogTests.Total(after)).IsEqualTo(NativeCatalogTests.Total(before));
            await Assert.That(NativeCatalogTests.ProductIds(after)).IsEquivalentTo(NativeCatalogTests.ProductIds(before));
            var category = await guest.InitializeAsync(categoryUrl);
            foreach (var html in new[] { after, category })
            {
                foreach (var marker in new[] { "sf-catalog-desktop-brand-", "sf-catalog-desktop-spec-", "sf-catalog-desktop-from" })
                    await Assert.That(html.Contains(marker, StringComparison.Ordinal)).IsFalse();
            }
        }
        finally
        {
            var errors = new List<Exception>();
            foreach (var row in originals)
            {
                try
                {
                    await UpdateAsync(admin, row, row.GetProperty("Value").GetString()!);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            if (errors.Count > 0)
                throw new AggregateException("Catalog settings could not all be restored.", errors);
        }
        var restored = await guest.InitializeAsync("catalog/all");
        await Assert.That(restored.Contains("sf-catalog-desktop-brand-", StringComparison.Ordinal)).IsTrue();
        await Assert.That(restored.Contains("sf-catalog-desktop-spec-", StringComparison.Ordinal)).IsTrue();
        await Assert.That(restored.Contains("sf-catalog-desktop-from", StringComparison.Ordinal)).IsTrue();
    }

    private static async Task UpdateAsync(StorefrontClient admin, JsonElement row, string value)
    {
        using var response = await admin.PostFormAsync("Admin/Setting/SettingUpdate", new()
        {
            ["Id"] = row.GetProperty("Id").GetInt32().ToString(),
            ["Name"] = row.GetProperty("Name").GetString()!,
            ["Value"] = value,
            ["__RequestVerificationToken"] = admin.Token
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}