using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeProductPresentationTests
{
    [Test]
    public async Task NativeGalleryTierMinimumAndCartEditUseAdministrativeData()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await using var other = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var productId = 0;
        JsonElement? zoomSetting = null;
        try
        {
            await admin.InitializeAsync("Admin/Setting/AllSettings");
            using var settings = await admin.PostFormAsync("Admin/Setting/AllSettings", new()
            {
                ["SearchSettingName"] = "mediasettings.defaultpicturezoomenabled",
                ["Length"] = "100",
                ["__RequestVerificationToken"] = admin.Token
            });
            zoomSetting = (await StorefrontClient.ReadJsonAsync(settings)).GetProperty("Data").EnumerateArray()
                .Single(row => row.GetProperty("Name").GetString() == "mediasettings.defaultpicturezoomenabled"
                    && row.GetProperty("StoreId").GetInt32() == 0);
            await SetZoomAsync(admin, zoomSetting.Value, "True");
            await admin.InitializeAsync("Admin/Product/Create");
            using var created = await admin.PostFormAsync("Admin/Product/Create", new()
            {
                ["Name"] = "محصول آزمون گالری " + Guid.NewGuid().ToString("N"),
                ["ProductTypeId"] = "5",
                ["ProductTemplateId"] = "1",
                ["VisibleIndividually"] = "true",
                ["Published"] = "true",
                ["Price"] = "100000",
                ["OldPrice"] = "120000",
                ["Sku"] = "TUNIT-GALLERY",
                ["ShortDescription"] = "نمونهٔ گالری و قیمت خرید تعدادی",
                ["FullDescription"] = "<p data-native-description>توضیحات کامل آزمون محصول</p>",
                ["AllowCustomerReviews"] = "true",
                ["ManageInventoryMethodId"] = "1",
                ["DisplayStockAvailability"] = "true",
                ["DisplayStockQuantity"] = "true",
                ["StockQuantity"] = "8",
                ["IsShipEnabled"] = "true",
                ["OrderMinimumQuantity"] = "2",
                ["OrderMaximumQuantity"] = "10",
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            });
            await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            productId = int.Parse(Regex.Match(created.Headers.Location!.OriginalString, "/Edit/([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value);
            foreach (var image in new[] { "notebook.jpg", "planner.jpg" })
                await Assert.That((await admin.UploadProductPictureAsync(productId, image)).GetProperty("success").GetBoolean()).IsTrue();
            foreach (var tier in new[] { (Quantity: 1, Price: "100000"), (Quantity: 4, Price: "75000") })
            {
                await admin.InitializeAsync($"Admin/Product/TierPriceCreatePopup?productId={productId}");
                using var saved = await admin.PostFormAsync("Admin/Product/TierPriceCreatePopup", new()
                {
                    ["ProductId"] = productId.ToString(),
                    ["Quantity"] = tier.Quantity.ToString(),
                    ["Price"] = tier.Price,
                    ["CustomerRoleId"] = "0",
                    ["StoreId"] = "0",
                    ["save"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                });
                await Assert.That(saved.StatusCode).IsEqualTo(HttpStatusCode.OK);
            }
            var page = $"product/productdetails?productId={productId}";
            var html = WebUtility.HtmlDecode(await guest.InitializeAsync(page));
            await Assert.That(html.Contains("sf-product-thumbnails", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("بزرگ‌نمایی تصویر محصول", StringComparison.Ordinal)).IsTrue();
            await Assert.That(Regex.Matches(html, "aria-pressed=\"(?:true|false)\"", RegexOptions.IgnoreCase).Count).IsEqualTo(2);
            await Assert.That(html.Contains("sf-product-tiers", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("<td>1+</td>", StringComparison.Ordinal)).IsTrue();
            var tierPrice = Regex.Match(html, "<td>4\\+</td>\\s*<td>([^<]+)</td>").Groups[1].Value;
            await Assert.That(tierPrice.Length > 0).IsTrue();
            await Assert.That(html.Contains("data-native-description", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("TUNIT-GALLERY", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("id=\"product-details-form\"", StringComparison.Ordinal)).IsTrue();
            await Assert.That(EnteredQuantity(html, productId)).IsEqualTo(2);
            await Assert.That(html.Contains("min=\"2\"", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains("application/ld+json", StringComparison.Ordinal)).IsTrue();
            using var quoted = await guest.PostFormAsync($"shoppingcart/productdetails_attributechange/{productId}/true/true", Fields(guest, productId, 2));
            var quote = await StorefrontClient.ReadJsonAsync(quoted);
            await Assert.That(quote.GetProperty("basepricepangv").ValueKind).IsEqualTo(JsonValueKind.Null);
            using var rejectedMinimum = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", Fields(guest, productId, 1));
            await Assert.That((await StorefrontClient.ReadJsonAsync(rejectedMinimum)).GetProperty("success").GetBoolean()).IsFalse();
            using var added = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", Fields(guest, productId, 4));
            await Assert.That((await StorefrontClient.ReadJsonAsync(added)).GetProperty("success").GetBoolean()).IsTrue();
            var cart = await guest.NativeCartAsync();
            var line = cart.GetProperty("Lines")[0];
            // Native product and cart factories may use different culture group separators.
            await Assert.That(line.GetProperty("UnitPrice").GetString()!.Replace('٬', ',')).IsEqualTo(tierPrice.Replace('٬', ','));
            var itemId = line.GetProperty("Id").GetInt32();
            var edit = await guest.InitializeAsync(page + "&updatecartitemid=" + itemId);
            await Assert.That(EnteredQuantity(edit, productId)).IsEqualTo(4);
            await Assert.That(edit.Contains($"name=\"addtocart_{productId}.UpdatedShoppingCartItemId\" value=\"{itemId}\"", StringComparison.Ordinal)).IsTrue();
            using var updated = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", Fields(guest, productId, 2, itemId));
            await Assert.That((await StorefrontClient.ReadJsonAsync(updated)).GetProperty("success").GetBoolean()).IsTrue();
            await Assert.That((await guest.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            using var rejectedStock = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", Fields(guest, productId, 9, itemId));
            await Assert.That((await StorefrontClient.ReadJsonAsync(rejectedStock)).GetProperty("success").GetBoolean()).IsFalse();
            using var rejectedMaximum = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", Fields(guest, productId, 11, itemId));
            await Assert.That((await StorefrontClient.ReadJsonAsync(rejectedMaximum)).GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That((await guest.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await other.InitializeAsync(page);
            using var deniedEdit = await other.GetAsync(page + "&updatecartitemid=" + itemId);
            await Assert.That(deniedEdit.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            using var forged = await other.PostFormAsync($"addproducttocart/details/{productId}/1", Fields(other, productId, 2, itemId));
            await Assert.That((await StorefrontClient.ReadJsonAsync(forged)).GetProperty("success").GetBoolean()).IsTrue();
            await Assert.That((await guest.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await Assert.That((await other.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Id").GetInt32()).IsNotEqualTo(itemId);
            await WaitForOptionalUiInspectionAsync(productId);
        }
        finally
        {
            try
            {
                await guest.ClearCartAsync();
                await other.ClearCartAsync();
            }
            finally
            {
                try
                {
                    if (productId > 0)
                    {
                        await admin.InitializeAsync($"Admin/Product/Edit/{productId}");
                        using var pictures = await admin.PostFormAsync("Admin/Product/ProductPictureList", new()
                        {
                            ["ProductId"] = productId.ToString(),
                            ["Start"] = "0",
                            ["Length"] = "100",
                            ["__RequestVerificationToken"] = admin.Token
                        });
                        var pictureRows = await StorefrontClient.ReadJsonAsync(pictures);
                        foreach (var picture in pictureRows.GetProperty("Data").EnumerateArray())
                        {
                            using var removed = await admin.PostFormAsync("Admin/Product/ProductPictureDelete", new()
                            {
                                ["id"] = picture.GetProperty("Id").GetInt32().ToString(),
                                ["__RequestVerificationToken"] = admin.Token
                            });
                            removed.EnsureSuccessStatusCode();
                        }
                        using var deleted = await admin.PostFormAsync($"Admin/Product/Delete/{productId}", new()
                        {
                            ["__RequestVerificationToken"] = admin.Token
                        });
                        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
                    }
                }
                finally
                {
                    if (zoomSetting.HasValue)
                        await SetZoomAsync(admin, zoomSetting.Value, zoomSetting.Value.GetProperty("Value").GetString()!);
                }
            }
        }
    }

    private static async Task SetZoomAsync(StorefrontClient admin, JsonElement row, string value)
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

    private static int EnteredQuantity(string html, int productId)
    {
        return int.Parse(Regex.Match(html, $"name=\"addtocart_{productId}.EnteredQuantity\" value=\"([0-9]+)\"").Groups[1].Value);
    }

    private static Dictionary<string, string> Fields(StorefrontClient client, int productId, int quantity, int itemId = 0)
    {
        return new()
        {
            [$"addtocart_{productId}.EnteredQuantity"] = quantity.ToString(),
            [$"addtocart_{productId}.UpdatedShoppingCartItemId"] = itemId.ToString(),
            ["__RequestVerificationToken"] = client.Token
        };
    }

    private static async Task WaitForOptionalUiInspectionAsync(int productId)
    {
        var path = Environment.GetEnvironmentVariable("STOREFRONT_TEST_UI_GATE");
        if (string.IsNullOrEmpty(path))
            return;
        path = Path.GetFullPath(path);
        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The UI inspection gate must be in the local temporary directory.");
        await File.WriteAllTextAsync(path + ".ready", JsonSerializer.Serialize(new
        {
            ProductId = productId
        }));
        var until = DateTime.UtcNow.AddMinutes(8);
        while (!File.Exists(path + ".done"))
        {
            if (DateTime.UtcNow >= until)
                throw new TimeoutException("The product UI inspection did not release its temporary fixture.");
            await Task.Delay(250);
        }
    }
}