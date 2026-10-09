using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeProductCombinationPresentationTests
{
    [Test]
    public async Task NativeCombinationMetadataPicturesAndBasePriceFollowAdministrativeChanges()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var productId = 0;
        var attributeId = 0;
        var mappingId = 0;
        var errors = new List<Exception>();
        try
        {
            var createHtml = await admin.InitializeAsync("Admin/Product/Create");
            var unitId = Regex.Match(createHtml, "<select[^>]*name=\"BasepriceUnitId\"[^>]*>(.*?)</select>", RegexOptions.Singleline).Groups[1].Value;
            unitId = Regex.Match(unitId, "<option[^>]*value=\"([1-9][0-9]*)\"").Groups[1].Value;
            await Assert.That(unitId.Length > 0).IsTrue();
            var fields = new Dictionary<string, string>
            {
                ["Name"] = "آزمون اطلاعات ترکیب " + Guid.NewGuid().ToString("N"),
                ["ProductTypeId"] = "5",
                ["ProductTemplateId"] = "1",
                ["VisibleIndividually"] = "true",
                ["Published"] = "true",
                ["Price"] = "100000",
                ["ManageInventoryMethodId"] = "2",
                ["StockQuantity"] = "0",
                ["AllowAddingOnlyExistingAttributeCombinations"] = "true",
                ["DisplayAttributeCombinationImagesOnly"] = "true",
                ["OrderMinimumQuantity"] = "1",
                ["OrderMaximumQuantity"] = "10",
                ["Sku"] = "",
                ["ManufacturerPartNumber"] = "",
                ["Gtin"] = "",
                ["BasepriceEnabled"] = "true",
                ["BasepriceAmount"] = "1",
                ["BasepriceUnitId"] = unitId,
                ["BasepriceBaseAmount"] = "1",
                ["BasepriceBaseUnitId"] = unitId,
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            };
            using (var created = await admin.PostFormAsync("Admin/Product/Create", fields))
                productId = await RedirectIdAsync(created, "Edit");
            foreach (var image in new[] { "notebook.jpg", "planner.jpg", "pens.jpg" })
                await Assert.That((await admin.UploadProductPictureAsync(productId, image)).GetProperty("success").GetBoolean()).IsTrue();
            var pictures = (await GridAsync(admin, "Product/ProductPictureList", new()
            {
                ["ProductId"] = productId.ToString()
            })).EnumerateArray().ToArray();
            await Assert.That(pictures.Length).IsEqualTo(3);
            var pictureIds = pictures.Take(2).Select(picture => picture.GetProperty("PictureId").GetInt32()).ToArray();

            await admin.InitializeAsync("Admin/ProductAttribute/Create");
            using (var created = await admin.PostFormAsync("Admin/ProductAttribute/Create", new()
            {
                ["Name"] = "رنگ آزمون " + Guid.NewGuid().ToString("N"),
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            }))
                attributeId = await RedirectIdAsync(created, "Edit");
            await admin.InitializeAsync($"Admin/Product/ProductAttributeMappingCreate?productId={productId}");
            using (var created = await admin.PostFormAsync("Admin/Product/ProductAttributeMappingCreate", new()
            {
                ["ProductId"] = productId.ToString(),
                ["ProductAttributeId"] = attributeId.ToString(),
                ["AttributeControlTypeId"] = "40",
                ["IsRequired"] = "true",
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            }))
                mappingId = await RedirectIdAsync(created, "ProductAttributeMappingEdit");
            foreach (var (name, color) in new[] { ("ترکیب دارای اطلاعات", "#ef4444"), ("ترکیب خالی", "#2563eb"), ("بدون ترکیب", "#16a34a") })
            {
                var path = $"Admin/Product/ProductAttributeValueCreatePopup?productAttributeMappingId={mappingId}";
                await admin.InitializeAsync(path);
                using var created = await admin.PostFormAsync(path, new()
                {
                    ["ProductAttributeMappingId"] = mappingId.ToString(),
                    ["AttributeValueTypeId"] = "0",
                    ["Name"] = name,
                    ["ColorSquaresRgb"] = color,
                    ["Quantity"] = "1",
                    ["IsPreSelected"] = (color == "#ef4444").ToString(),
                    ["save"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                });
                await PopupSavedAsync(created);
            }
            var values = (await GridAsync(admin, "Product/ProductAttributeValueList", new()
            {
                ["ProductAttributeMappingId"] = mappingId.ToString()
            }))
                .EnumerateArray().ToDictionary(value => value.GetProperty("ColorSquaresRgb").GetString()!, value => value.GetProperty("Id").GetInt32());
            await Assert.That(values.Count).IsEqualTo(3);
            var populatedValue = values["#ef4444"];
            var blankValue = values["#2563eb"];
            var missingValue = values["#16a34a"];
            foreach (var value in new[] { populatedValue, blankValue })
            {
                var path = $"Admin/Product/ProductAttributeCombinationCreatePopup?productId={productId}";
                await admin.InitializeAsync(path);
                var combination = new Dictionary<string, string>
                {
                    ["ProductId"] = productId.ToString(),
                    [$"product_attribute_{mappingId}"] = value.ToString(),
                    ["StockQuantity"] = "5",
                    ["Sku"] = value == populatedValue ? "COMBO-SKU" : "",
                    ["ManufacturerPartNumber"] = value == populatedValue ? "COMBO-MPN" : "",
                    ["Gtin"] = value == populatedValue ? "1234567890123" : "",
                    ["save"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                };
                if (value == populatedValue)
                {
                    for (var index = 0; index < pictureIds.Length; index++)
                        combination[$"PictureIds[{index}]"] = pictureIds[index].ToString();
                }
                using var created = await admin.PostFormAsync(path, combination);
                await PopupSavedAsync(created);
            }
            var combinations = await GridAsync(admin, "Product/ProductAttributeCombinationList", new()
            {
                ["ProductId"] = productId.ToString()
            });
            await Assert.That(combinations.GetArrayLength()).IsEqualTo(2);
            var populated = combinations.EnumerateArray().Single(row => row.GetProperty("Sku").GetString() == "COMBO-SKU");
            var editCombination = await admin.InitializeAsync("Admin/Product/ProductAttributeCombinationEditPopup/" + populated.GetProperty("Id").GetInt32());
            var savedPictures = Regex.Matches(editCombination, "<input[^>]*name=\"PictureIds\"[^>]*>")
                .Cast<Match>().Where(match => match.Value.Contains("checked", StringComparison.Ordinal))
                .Select(match => int.Parse(Regex.Match(match.Value, "value=\"([0-9]+)\"").Groups[1].Value)).ToArray();
            await Assert.That(savedPictures).IsEquivalentTo(pictureIds);

            var page = $"product/productdetails?productId={productId}";
            var html = await guest.InitializeAsync(page);
            var initial = InitialProduct(html, productId);
            await Assert.That(initial.GetProperty("combinationImagesOnly").GetBoolean()).IsTrue();
            await Assert.That(initial.GetProperty("pictures").GetArrayLength()).IsEqualTo(3);
            await Assert.That(initial.GetProperty("image").GetString()).IsEqualTo(initial.GetProperty("pictures")[0].GetProperty("image").GetString());
            await Assert.That(initial.GetProperty("fullSizeImage").GetString()).IsEqualTo(initial.GetProperty("pictures")[0].GetProperty("fullSize").GetString());
            await Assert.That(initial.GetProperty("sku").GetString()).IsEqualTo("");
            await Assert.That(initial.GetProperty("mpn").GetString()).IsEqualTo("");
            await Assert.That(initial.GetProperty("gtin").GetString()).IsEqualTo("");
            await Assert.That(initial.GetProperty("basePrice").GetString()!.Length > 0).IsTrue();
            var thumbnails = Regex.Match(html, "<div class=\"sf-product-thumbnails\".*?</div>", RegexOptions.Singleline).Value;
            await Assert.That(Regex.Matches(thumbnails, "<img\\b").Count).IsEqualTo(3);
            await Assert.That(html.Contains("data-product-sku", StringComparison.Ordinal)).IsFalse();

            var quote = await QuoteAsync(guest, productId, mappingId, populatedValue);
            await Assert.That(quote.GetProperty("sku").GetString()).IsEqualTo("COMBO-SKU");
            await Assert.That(quote.GetProperty("mpn").GetString()).IsEqualTo("COMBO-MPN");
            await Assert.That(quote.GetProperty("gtin").GetString()).IsEqualTo("1234567890123");
            await Assert.That(quote.GetProperty("pictureIds").EnumerateArray().Select(id => id.GetInt32())).IsEquivalentTo(pictureIds);
            await Assert.That(quote.GetProperty("basepricepangv").GetString()!.Length > 0).IsTrue();
            var firstPicture = initial.GetProperty("pictures").EnumerateArray().Single(picture => picture.GetProperty("id").GetInt32() == quote.GetProperty("pictureIds")[0].GetInt32());
            await Assert.That(quote.GetProperty("pictureDefaultSizeUrl").GetString()).IsEqualTo(firstPicture.GetProperty("image").GetString());
            await Assert.That(quote.GetProperty("pictureFullSizeUrl").GetString()).IsEqualTo(firstPicture.GetProperty("fullSize").GetString());
            foreach (var value in new[] { blankValue, missingValue })
            {
                await Assert.That((await QuoteAsync(guest, productId, mappingId, populatedValue)).GetProperty("sku").GetString()).IsEqualTo("COMBO-SKU");
                var empty = await QuoteAsync(guest, productId, mappingId, value);
                foreach (var name in new[] { "sku", "mpn", "gtin", "pictureDefaultSizeUrl", "pictureFullSizeUrl" })
                    await Assert.That(string.IsNullOrEmpty(empty.GetProperty(name).GetString())).IsTrue();
                await Assert.That(empty.GetProperty("pictureIds").GetArrayLength()).IsEqualTo(0);
            }

            fields["Sku"] = "BASE-SKU";
            fields["ManufacturerPartNumber"] = "BASE-MPN";
            fields["Gtin"] = "9876543210123";
            await EditProductAsync(admin, productId, fields);
            foreach (var value in new[] { blankValue, missingValue })
            {
                var fallback = await QuoteAsync(guest, productId, mappingId, value);
                await Assert.That(fallback.GetProperty("sku").GetString()).IsEqualTo(fields["Sku"]);
                await Assert.That(fallback.GetProperty("mpn").GetString()).IsEqualTo(fields["ManufacturerPartNumber"]);
                await Assert.That(fallback.GetProperty("gtin").GetString()).IsEqualTo(fields["Gtin"]);
            }
            fields["Sku"] = fields["ManufacturerPartNumber"] = fields["Gtin"] = "";
            foreach (var enabled in new[] { false, true })
            {
                fields["DisplayAttributeCombinationImagesOnly"] = enabled.ToString();
                await EditProductAsync(admin, productId, fields);
                await Assert.That(InitialProduct(await guest.InitializeAsync(page), productId).GetProperty("combinationImagesOnly").GetBoolean()).IsEqualTo(enabled);
                await Assert.That((await QuoteAsync(guest, productId, mappingId, populatedValue)).GetProperty("pictureIds")
                    .EnumerateArray().Select(id => id.GetInt32())).IsEquivalentTo(pictureIds);
            }
            foreach (var enabled in new[] { false, true, false })
            {
                fields["BasepriceEnabled"] = enabled.ToString();
                await EditProductAsync(admin, productId, fields);
                // Keep the same guest session and token; no storefront reload between the edit and native quote.
                var changed = await QuoteAsync(guest, productId, mappingId, populatedValue);
                if (enabled)
                    await Assert.That(changed.GetProperty("basepricepangv").GetString()!.Length > 0).IsTrue();
                else
                    await Assert.That(changed.GetProperty("basepricepangv").ValueKind).IsEqualTo(JsonValueKind.Null);
            }
        }
        catch (Exception error)
        {
            errors.Add(error);
        }
        finally
        {
            // Each independent cleanup step runs even if an earlier step fails.
            if (productId > 0)
            {
                await CleanupAsync(errors, async () =>
                {
                    var rows = await GridAsync(admin, "Product/ProductAttributeCombinationList", new()
                    {
                        ["ProductId"] = productId.ToString()
                    });
                    foreach (var row in rows.EnumerateArray())
                        await CleanupAsync(errors, () => DeleteRowAsync(admin, "ProductAttributeCombinationDelete", row.GetProperty("Id").GetInt32()));
                    await Assert.That((await GridAsync(admin, "Product/ProductAttributeCombinationList", new()
                    {
                        ["ProductId"] = productId.ToString()
                    })).GetArrayLength()).IsEqualTo(0);
                });
                if (mappingId > 0)
                {
                    await CleanupAsync(errors, async () =>
                    {
                        using var removed = await admin.PostFormAsync("Admin/Product/ProductAttributeMappingDelete", TokenFields(admin, mappingId));
                        await Assert.That(removed.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
                        var rows = await GridAsync(admin, "Product/ProductAttributeMappingList", new()
                        {
                            ["ProductId"] = productId.ToString()
                        });
                        await Assert.That(rows.EnumerateArray().Any(row => row.GetProperty("Id").GetInt32() == mappingId)).IsFalse();
                    });
                }
                await CleanupAsync(errors, async () =>
                {
                    // Do not delete assets when their combination/value associations could remain.
                    var combinations = await GridAsync(admin, "Product/ProductAttributeCombinationList", new()
                    {
                        ["ProductId"] = productId.ToString()
                    });
                    var mappings = await GridAsync(admin, "Product/ProductAttributeMappingList", new()
                    {
                        ["ProductId"] = productId.ToString()
                    });
                    await Assert.That(combinations.GetArrayLength()).IsEqualTo(0);
                    await Assert.That(mappings.GetArrayLength()).IsEqualTo(0);
                    var rows = await GridAsync(admin, "Product/ProductPictureList", new()
                    {
                        ["ProductId"] = productId.ToString()
                    });
                    foreach (var row in rows.EnumerateArray())
                        await CleanupAsync(errors, () => DeleteRowAsync(admin, "ProductPictureDelete", row.GetProperty("Id").GetInt32()));
                    await Assert.That((await GridAsync(admin, "Product/ProductPictureList", new()
                    {
                        ["ProductId"] = productId.ToString()
                    })).GetArrayLength()).IsEqualTo(0);
                });
                await CleanupAsync(errors, () => DeleteEntityAsync(admin, "Product", productId));
            }
            if (attributeId > 0)
                await CleanupAsync(errors, () => DeleteEntityAsync(admin, "ProductAttribute", attributeId));
        }
        if (errors.Count > 0)
            throw new AggregateException("Combination fixture failed or its owned resources could not be cleaned up.", errors);
    }

    private static async Task<JsonElement> QuoteAsync(StorefrontClient guest, int productId, int mappingId, int valueId)
    {
        using var response = await guest.PostFormAsync($"shoppingcart/productdetails_attributechange/{productId}/true/true", new()
        {
            [$"product_attribute_{mappingId}"] = valueId.ToString(),
            [$"addtocart_{productId}.EnteredQuantity"] = "1",
            ["__RequestVerificationToken"] = guest.Token
        });
        return await StorefrontClient.ReadJsonAsync(response);
    }

    private static JsonElement InitialProduct(string html, int productId)
    {
        foreach (Match match in Regex.Matches(html, "<!--Blazor:(.*?)-->", RegexOptions.Singleline))
        {
            using var marker = JsonDocument.Parse(match.Groups[1].Value);
            if (!marker.RootElement.TryGetProperty("parameterValues", out var values))
                continue;
            using var parameters = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(values.GetString()!)));
            foreach (var parameter in parameters.RootElement.EnumerateArray())
            {
                if (parameter.ValueKind == JsonValueKind.Object && parameter.TryGetProperty("combinationImagesOnly", out _)
                    && parameter.GetProperty("id").GetInt32() == productId)
                    return parameter.Clone();
            }
        }
        throw new InvalidOperationException("The native prerendered product did not provide its browser parameters.");
    }

    private static async Task EditProductAsync(StorefrontClient admin, int productId, Dictionary<string, string> fields)
    {
        var html = await admin.InitializeAsync("Admin/Product/Edit/" + productId);
        fields["Id"] = productId.ToString();
        fields["LastStockQuantity"] = Regex.Match(html, "name=\"LastStockQuantity\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
        fields["__RequestVerificationToken"] = admin.Token;
        using var saved = await admin.PostFormAsync("Admin/Product/Edit/" + productId, fields);
        await Assert.That(await RedirectIdAsync(saved, "Edit")).IsEqualTo(productId);
        html = await admin.InitializeAsync("Admin/Product/Edit/" + productId);
        foreach (var name in new[] { "Sku", "ManufacturerPartNumber", "Gtin", "LastStockQuantity" })
        {
            var value = Regex.Match(html, "<input[^>]*name=\"" + name + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value;
            await Assert.That(WebUtility.HtmlDecode(value)).IsEqualTo(fields[name]);
        }
        foreach (var name in new[] { "DisplayAttributeCombinationImagesOnly", "BasepriceEnabled" })
        {
            var checkbox = Regex.Match(html, "<input[^>]*type=\"checkbox\"[^>]*name=\"" + name + "\"[^>]*>").Value;
            if (checkbox.Length == 0)
                checkbox = Regex.Match(html, "<input[^>]*name=\"" + name + "\"[^>]*type=\"checkbox\"[^>]*>").Value;
            await Assert.That(checkbox.Length > 0).IsTrue();
            await Assert.That(checkbox.Contains("checked", StringComparison.Ordinal)).IsEqualTo(bool.Parse(fields[name]));
        }
    }

    private static async Task<JsonElement> GridAsync(StorefrontClient admin, string action, Dictionary<string, string> fields)
    {
        fields["Start"] = "0";
        fields["Length"] = "100";
        fields["__RequestVerificationToken"] = admin.Token;
        using var response = await admin.PostFormAsync("Admin/" + action, fields);
        return (await StorefrontClient.ReadJsonAsync(response)).GetProperty("Data");
    }

    private static async Task<int> RedirectIdAsync(HttpResponseMessage response, string action)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var match = Regex.Match(response.Headers.Location!.OriginalString, "/" + action + "/([0-9]+)", RegexOptions.IgnoreCase);
        await Assert.That(match.Success).IsTrue();
        return int.Parse(match.Groups[1].Value);
    }

    private static async Task PopupSavedAsync(HttpResponseMessage response)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await response.Content.ReadAsStringAsync()).Contains("window.close()", StringComparison.Ordinal)).IsTrue();
    }

    private static Dictionary<string, string> TokenFields(StorefrontClient admin, int id)
    {
        return new()
        {
            ["id"] = id.ToString(),
            ["__RequestVerificationToken"] = admin.Token
        };
    }

    private static async Task DeleteRowAsync(StorefrontClient admin, string action, int id)
    {
        using var response = await admin.PostFormAsync("Admin/Product/" + action, TokenFields(admin, id));
        response.EnsureSuccessStatusCode();
    }

    private static async Task DeleteEntityAsync(StorefrontClient admin, string controller, int id)
    {
        await admin.InitializeAsync($"Admin/{controller}/Edit/{id}");
        using var response = await admin.PostFormAsync($"Admin/{controller}/Delete/{id}", TokenFields(admin, id));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        await Assert.That(response.Headers.Location!.OriginalString.TrimEnd('/')).IsEqualTo($"/Admin/{controller}/List");
    }

    private static async Task CleanupAsync(List<Exception> errors, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception error)
        {
            errors.Add(error);
        }
    }
}