using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeProductPolicyTests
{
    [Test]
    public async Task AppliedDiscountAndCallForPriceKeepNativePurchaseRules()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var products = new List<int>();
        var discountId = 0;
        try
        {
            await admin.InitializeAsync("Admin/Discount/Create");
            using var created = await admin.PostFormAsync("Admin/Discount/Create", new()
            {
                ["Name"] = "TUnit applied discount " + Guid.NewGuid().ToString("N"),
                ["DiscountTypeId"] = "2",
                ["IsActive"] = "true",
                ["UsePercentage"] = "true",
                ["DiscountPercentage"] = "20",
                ["DiscountLimitationId"] = "0",
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            });
            discountId = await RedirectIdAsync(created);
            var productId = await CreateProductAsync(admin, new()
            {
                ["SelectedDiscountIds[0]"] = discountId.ToString()
            });
            products.Add(productId);
            var html = WebUtility.HtmlDecode(await guest.InitializeAsync(ProductPage(productId)));
            var card = PriceCard(html);
            var price = Regex.Match(card, "<strong[^>]*class=\"[^\"]*\\bprice\\b[^\"]*\"[^>]*>([^<]+)</strong>").Groups[1].Value;
            await Assert.That(price.Length > 0).IsTrue();
            await Assert.That(Amount(price)).IsEqualTo("80000");
            await Assert.That(Regex.Matches(card, "<del[^>]*>([^<]+)</del>").Select(match => Amount(match.Groups[1].Value))).IsEquivalentTo(new[] { "130000", "100000" });
            using var quoted = await guest.PostFormAsync($"shoppingcart/productdetails_attributechange/{productId}/true/true", PurchaseFields(guest, productId));
            await Assert.That(NormalizePrice((await StorefrontClient.ReadJsonAsync(quoted)).GetProperty("price").GetString()!)).IsEqualTo(NormalizePrice(price));
            using var added = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", PurchaseFields(guest, productId));
            await Assert.That((await StorefrontClient.ReadJsonAsync(added)).GetProperty("success").GetBoolean()).IsTrue();
            var before = await guest.NativeCartAsync();
            await Assert.That(NormalizePrice(before.GetProperty("Lines")[0].GetProperty("UnitPrice").GetString()!)).IsEqualTo(NormalizePrice(price));

            var callId = await CreateProductAsync(admin, new()
            {
                ["CallForPrice"] = "true"
            });
            products.Add(callId);
            var callHtml = WebUtility.HtmlDecode(await guest.InitializeAsync(ProductPage(callId)));
            var callCard = PriceCard(callHtml);
            var callLabels = await LocalizedNamesAsync(admin, "Products.CallForPrice", "Call for pricing");
            await Assert.That(callLabels.Any(label => callCard.Contains(label, StringComparison.Ordinal))).IsTrue();
            await Assert.That(callCard.Contains("<strong", StringComparison.Ordinal)).IsFalse();
            await Assert.That(callCard.Contains("<del", StringComparison.Ordinal)).IsFalse();
            using var refused = await guest.PostFormAsync($"addproducttocart/details/{callId}/1", PurchaseFields(guest, callId));
            var result = await StorefrontClient.ReadJsonAsync(refused);
            await Assert.That(result.GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That(result.GetProperty("message").GetArrayLength() > 0).IsTrue();
            await Assert.That(result.GetProperty("message").EnumerateArray().Any(message => callLabels.Contains(message.GetString()!))).IsTrue();
            var after = await guest.NativeCartAsync();
            await Assert.That(after.GetProperty("Lines").GetArrayLength()).IsEqualTo(1);
            await Assert.That(after.GetProperty("Lines")[0].GetProperty("Id").GetInt32()).IsEqualTo(before.GetProperty("Lines")[0].GetProperty("Id").GetInt32());
            await WaitForUiAsync(new
            {
                DiscountProductId = productId,
                CallProductId = callId
            });
        }
        finally
        {
            try
            {
                await guest.ClearCartAsync();
            }
            finally
            {
                try
                {
                    try
                    {
                        if (products.Count > 0)
                            await DeleteAsync(admin, "Product", products[0]);
                    }
                    finally
                    {
                        if (products.Count > 1)
                            await DeleteAsync(admin, "Product", products[1]);
                    }
                }
                finally { if (discountId > 0) await DeleteAsync(admin, "Discount", discountId); }
            }
        }
    }

    [Test]
    public async Task GuestPricePermissionHidesPricesAndRestoresExactRoleMappings()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var productId = 0;
        JsonElement? permission = null;
        try
        {
            productId = await CreateProductAsync(admin, new());
            await admin.InitializeAsync("Admin/Product/TierPriceCreatePopup?productId=" + productId);
            using var tier = await admin.PostFormAsync("Admin/Product/TierPriceCreatePopup", new()
            {
                ["ProductId"] = productId.ToString(),
                ["Quantity"] = "4",
                ["Price"] = "75000",
                ["CustomerRoleId"] = "0",
                ["StoreId"] = "0",
                ["save"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            });
            await Assert.That(tier.StatusCode).IsEqualTo(HttpStatusCode.OK);
            var visibleHtml = WebUtility.HtmlDecode(await guest.InitializeAsync(ProductPage(productId)));
            await Assert.That(visibleHtml.Contains("sf-product-tiers", StringComparison.Ordinal)).IsTrue();
            var visible = PriceCard(visibleHtml);
            await Assert.That(visible.Contains("<strong", StringComparison.Ordinal)).IsTrue();
            await admin.InitializeAsync("Admin/CustomerRole/List");
            var roles = await GridAsync(admin, "Admin/CustomerRole/List", new());
            var guestRole = roles.EnumerateArray().Single(row => row.GetProperty("SystemName").GetString() == "Guests").GetProperty("Id").GetInt32();
            var names = await LocalizedNamesAsync(admin, "Security.Permission.PublicStore.DisplayPrices", "Public store. Display Prices");
            await admin.InitializeAsync("Admin/Security/Permissions");
            var permissions = await GridAsync(admin, "Admin/Security/PermissionCategory", new()
            {
                ["PermissionCategoryName"] = "PublicStore"
            });
            permission = permissions.EnumerateArray().Single(row => names.Contains(row.GetProperty("PermissionName").GetString()!));
            var original = permission.Value.GetProperty("SelectedCustomerRoleIds").EnumerateArray().Select(id => id.GetInt32()).ToArray();
            await Assert.That(original.Contains(guestRole)).IsTrue();
            await SetPermissionRolesAsync(admin, permission.Value, original.Where(id => id != guestRole).ToArray());
            var hidden = WebUtility.HtmlDecode(await guest.InitializeAsync(ProductPage(productId)));
            await Assert.That(hidden.Contains("sf-product-name", StringComparison.Ordinal)).IsTrue();
            await Assert.That(hidden.Contains("sf-price-card", StringComparison.Ordinal)).IsFalse();
            await Assert.That(hidden.Contains("sf-product-quantity-input", StringComparison.Ordinal)).IsFalse();
            await Assert.That(hidden.Contains("این انتخاب فعلاً ناموجود است", StringComparison.Ordinal)).IsFalse();
            await Assert.That(hidden.Contains("فعلاً ناموجود", StringComparison.Ordinal)).IsFalse();
            await Assert.That(hidden.Contains("sf-product-tiers", StringComparison.Ordinal)).IsFalse();
            using var quote = await guest.PostFormAsync($"shoppingcart/productdetails_attributechange/{productId}/true/true", PurchaseFields(guest, productId));
            await Assert.That((await StorefrontClient.ReadJsonAsync(quote)).GetProperty("price").GetString()).IsEqualTo("");
            // The optional browser session may already be an ordinary registered customer.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STOREFRONT_TEST_UI_GATE")))
            {
                var registeredRole = roles.EnumerateArray().Single(row => row.GetProperty("SystemName").GetString() == "Registered").GetProperty("Id").GetInt32();
                await SetPermissionRolesAsync(admin, permission.Value, original.Where(id => id != guestRole && id != registeredRole).ToArray());
            }
            await WaitForUiAsync(new
            {
                HiddenPriceProductId = productId
            });
        }
        finally
        {
            try
            {
                if (permission.HasValue)
                {
                    var original = permission.Value.GetProperty("SelectedCustomerRoleIds").EnumerateArray().Select(id => id.GetInt32()).ToArray();
                    await SetPermissionRolesAsync(admin, permission.Value, original);
                    var restored = PriceCard(WebUtility.HtmlDecode(await guest.InitializeAsync(ProductPage(productId))));
                    await Assert.That(restored.Contains("<strong", StringComparison.Ordinal)).IsTrue();
                    var current = await GridAsync(admin, "Admin/Security/PermissionCategory", new()
                    {
                        ["PermissionCategoryName"] = "PublicStore"
                    });
                    var row = current.EnumerateArray().Single(item => item.GetProperty("Id").GetInt32() == permission.Value.GetProperty("Id").GetInt32());
                    await Assert.That(row.GetProperty("SelectedCustomerRoleIds").EnumerateArray().Select(id => id.GetInt32())).IsEquivalentTo(original);
                }
            }
            finally { if (productId > 0) await DeleteAsync(admin, "Product", productId); }
        }
    }

    [Test]
    public async Task UnsupportedPurchaseControlsKeepNativeFormsAfterInvalidReview()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        foreach (var kind in new[] { "Rental", "GiftCard", "CustomerPrice", "Text", "Checkbox", "File", "AttributeQuantity" })
        {
            var productId = 0;
            var attributeId = 0;
            var mappingId = 0;
            try
            {
                var fields = new Dictionary<string, string>();
                switch (kind)
                {
                    case "Rental":
                        fields["IsRental"] = "true";
                        fields["RentalPriceLength"] = "1";
                        fields["RentalPricePeriodId"] = "0";
                        break;
                    case "GiftCard":
                        fields["IsGiftCard"] = "true";
                        fields["GiftCardTypeId"] = "0";
                        break;
                    case "CustomerPrice":
                        fields["CustomerEntersPrice"] = "true";
                        fields["MinimumCustomerEnteredPrice"] = "1000";
                        fields["MaximumCustomerEnteredPrice"] = "200000";
                        break;
                }
                productId = await CreateProductAsync(admin, fields);
                if (kind is "Text" or "Checkbox" or "File" or "AttributeQuantity")
                {
                    await admin.InitializeAsync("Admin/ProductAttribute/Create");
                    using var attribute = await admin.PostFormAsync("Admin/ProductAttribute/Create", new()
                    {
                        ["Name"] = "TUnit fallback " + Guid.NewGuid().ToString("N"),
                        ["save-continue"] = "true",
                        ["__RequestVerificationToken"] = admin.Token
                    });
                    attributeId = await RedirectIdAsync(attribute);
                    var controlType = kind switch
                    {
                        "Text" => 4,
                        "Checkbox" => 3,
                        "File" => 30,
                        _ => 1
                    };
                    await admin.InitializeAsync("Admin/Product/ProductAttributeMappingCreate?productId=" + productId);
                    using var mapping = await admin.PostFormAsync("Admin/Product/ProductAttributeMappingCreate", new()
                    {
                        ["ProductId"] = productId.ToString(),
                        ["ProductAttributeId"] = attributeId.ToString(),
                        ["AttributeControlTypeId"] = controlType.ToString(),
                        ["IsRequired"] = "true",
                        ["DefaultValue"] = "fallback default",
                        ["ValidationFileAllowedExtensions"] = "txt",
                        ["ValidationFileMaximumSize"] = "1024",
                        ["save-continue"] = "true",
                        ["__RequestVerificationToken"] = admin.Token
                    });
                    mappingId = await RedirectIdAsync(mapping, "ProductAttributeMappingEdit");
                    if (kind is "Checkbox" or "AttributeQuantity")
                    {
                        for (var i = 0; i < (kind == "Checkbox" ? 2 : 1); i++)
                        {
                            await admin.InitializeAsync("Admin/Product/ProductAttributeValueCreatePopup?productAttributeMappingId=" + mappingId);
                            using var value = await admin.PostFormAsync("Admin/Product/ProductAttributeValueCreatePopup", new()
                            {
                                ["ProductAttributeMappingId"] = mappingId.ToString(),
                                ["Name"] = "fallback option " + i,
                                ["AttributeValueTypeId"] = "0",
                                ["Quantity"] = "1",
                                ["IsPreSelected"] = "true",
                                ["CustomerEntersQty"] = (kind == "AttributeQuantity").ToString(),
                                ["__RequestVerificationToken"] = admin.Token
                            });
                            await Assert.That(value.StatusCode).IsEqualTo(HttpStatusCode.OK);
                        }
                    }
                }
                var html = WebUtility.HtmlDecode(await guest.InitializeAsync(ProductPage(productId)));
                await AssertFallbackAsync(html, kind, productId, mappingId);
                await admin.InitializeAsync(ProductPage(productId));
                using var invalidReview = await admin.PostFormAsync("product/productreviews?productId=" + productId, new()
                {
                    ["ProductId"] = productId.ToString(),
                    ["AddProductReview.Title"] = "",
                    ["AddProductReview.ReviewText"] = "fallback review marker",
                    ["AddProductReview.Rating"] = "4",
                    ["__RequestVerificationToken"] = admin.Token
                });
                await Assert.That(invalidReview.StatusCode).IsEqualTo(HttpStatusCode.OK);
                var redisplayed = WebUtility.HtmlDecode(await invalidReview.Content.ReadAsStringAsync());
                await AssertFallbackAsync(redisplayed, kind, productId, mappingId);
                await Assert.That(Regex.IsMatch(redisplayed, "class=\"field-validation-error\"[^>]*data-valmsg-for=\"AddProductReview.Title\"", RegexOptions.IgnoreCase)).IsTrue();
            }
            finally
            {
                try
                {
                    if (mappingId > 0)
                    {
                        await admin.InitializeAsync("Admin/Product/ProductAttributeMappingEdit/" + mappingId);
                        using var deleted = await admin.PostFormAsync("Admin/Product/ProductAttributeMappingDelete/" + mappingId, new()
                        {
                            ["__RequestVerificationToken"] = admin.Token
                        });
                        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
                    }
                }
                finally
                {
                    try
                    {
                        if (attributeId > 0)
                            await DeleteAsync(admin, "ProductAttribute", attributeId);
                    }
                    finally { if (productId > 0) await DeleteAsync(admin, "Product", productId); }
                }
            }
        }
    }

    private static async Task AssertFallbackAsync(string html, string kind, int productId, int mappingId)
    {
        await Assert.That(html.Contains("id=\"product-details-form\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("sf-product-name", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("sf-price-card", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("id=\"add-to-cart-button-" + productId + "\"", StringComparison.Ordinal)).IsTrue();
        var nativeName = "product_attribute_" + mappingId;
        switch (kind)
        {
            case "Rental":
                await Assert.That(html.Contains("name=\"rental_start_date_" + productId + "\"", StringComparison.Ordinal)).IsTrue();
                await Assert.That(html.Contains("name=\"rental_end_date_" + productId + "\"", StringComparison.Ordinal)).IsTrue();
                break;
            case "GiftCard":
                foreach (var field in new[] { "RecipientName", "RecipientEmail", "SenderName", "SenderEmail" })
                    await Assert.That(html.Contains($"name=\"giftcard_{productId}.{field}\"", StringComparison.Ordinal)).IsTrue();
                break;
            case "CustomerPrice":
                await Assert.That(html.Contains($"name=\"addtocart_{productId}.CustomerEnteredPrice\"", StringComparison.Ordinal)).IsTrue();
                break;
            case "Checkbox":
                await Assert.That(Regex.Matches(html, $"<input[^>]*type=\"checkbox\"[^>]*name=\"{nativeName}\"").Count).IsEqualTo(2);
                break;
            case "File":
                await Assert.That(html.Contains("id=\"" + nativeName + "element\"", StringComparison.Ordinal)).IsTrue();
                await Assert.That(html.Contains("FilePond", StringComparison.Ordinal)).IsTrue();
                await Assert.That(html.Contains("name=\"" + nativeName + "\"", StringComparison.Ordinal)).IsTrue();
                break;
            case "AttributeQuantity":
                await Assert.That(Regex.IsMatch(html, $"name=\"{nativeName}_[0-9]+_qty\"")).IsTrue();
                break;
            case "Text":
                await Assert.That(Regex.IsMatch(html, $"<input[^>]*name=\"{nativeName}\"[^>]*type=\"text\"")).IsTrue();
                break;
        }
    }

    private static async Task<int> CreateProductAsync(StorefrontClient admin, Dictionary<string, string> overrides)
    {
        await admin.InitializeAsync("Admin/Product/Create");
        var fields = new Dictionary<string, string>
        {
            ["Name"] = "آزمون سیاست محصول " + Guid.NewGuid().ToString("N"),
            ["ProductTypeId"] = "5",
            ["ProductTemplateId"] = "1",
            ["Published"] = "true",
            ["VisibleIndividually"] = "true",
            ["Price"] = "100000",
            ["OldPrice"] = "130000",
            ["ManageInventoryMethodId"] = "0",
            ["OrderMinimumQuantity"] = "1",
            ["OrderMaximumQuantity"] = "10",
            ["AllowCustomerReviews"] = "true",
            ["save-continue"] = "true",
            ["__RequestVerificationToken"] = admin.Token
        };
        foreach (var field in overrides)
            fields[field.Key] = field.Value;
        using var response = await admin.PostFormAsync("Admin/Product/Create", fields);
        return await RedirectIdAsync(response);
    }

    private static async Task<int> RedirectIdAsync(HttpResponseMessage response, string action = "Edit")
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var match = Regex.Match(response.Headers.Location!.OriginalString, "/" + action + "/([0-9]+)", RegexOptions.IgnoreCase);
        await Assert.That(match.Success).IsTrue();
        return int.Parse(match.Groups[1].Value);
    }

    private static async Task DeleteAsync(StorefrontClient admin, string controller, int id)
    {
        await admin.InitializeAsync($"Admin/{controller}/Edit/{id}");
        using var response = await admin.PostFormAsync($"Admin/{controller}/Delete/{id}", new()
        {
            ["__RequestVerificationToken"] = admin.Token
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
    }

    private static async Task<JsonElement> GridAsync(StorefrontClient admin, string path, Dictionary<string, string> fields)
    {
        fields["Start"] = "0";
        fields["Length"] = "100";
        fields["__RequestVerificationToken"] = admin.Token;
        using var response = await admin.PostFormAsync(path, fields);
        return (await StorefrontClient.ReadJsonAsync(response)).GetProperty("Data");
    }

    private static async Task SetPermissionRolesAsync(StorefrontClient admin, JsonElement row, int[] ids)
    {
        await admin.InitializeAsync("Admin/Security/PermissionEditPopup/" + row.GetProperty("Id").GetInt32());
        var fields = new Dictionary<string, string> { ["Id"] = row.GetProperty("Id").GetInt32().ToString(), ["__RequestVerificationToken"] = admin.Token };
        for (var i = 0; i < ids.Length; i++)
            fields[$"SelectedCustomerRoleIds[{i}]"] = ids[i].ToString();
        using var response = await admin.PostFormAsync("Admin/Security/PermissionEditPopup", fields);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static async Task<HashSet<string>> LocalizedNamesAsync(StorefrontClient admin, string resourceName, string defaultName)
    {
        await admin.InitializeAsync("Admin/Language/List");
        var languages = await GridAsync(admin, "Admin/Language/List", new());
        var names = new HashSet<string>(StringComparer.Ordinal) { defaultName };
        foreach (var language in languages.EnumerateArray())
        {
            var resources = await GridAsync(admin, "Admin/Language/Resources", new()
            {
                ["LanguageId"] = language.GetProperty("Id").GetInt32().ToString(),
                ["SearchResourceName"] = resourceName
            });
            foreach (var resource in resources.EnumerateArray().Where(row => string.Equals(row.GetProperty("ResourceName").GetString(), resourceName, StringComparison.OrdinalIgnoreCase)))
            {
                var value = resource.GetProperty("ResourceValue").GetString();
                if (!string.IsNullOrEmpty(value))
                    names.Add(value);
            }
        }
        return names;
    }

    private static string ProductPage(int id)
    {
        return "product/productdetails?productId=" + id;
    }
    private static string NormalizePrice(string price)
    {
        return price.Replace('٬', ',');
    }
    private static string Amount(string price)
    {
        return Regex.Replace(price, "[^0-9]", "");
    }
    private static string PriceCard(string html)
    {
        var match = Regex.Match(html, "<div[^>]*class=\"[^\"]*\\bsf-price-card\\b[^\"]*\"[^>]*>(.*?)</button>\\s*</div>", RegexOptions.Singleline);
        if (!match.Success)
            throw new InvalidOperationException("Native BB price card missing.");
        return match.Groups[1].Value;
    }

    private static Dictionary<string, string> PurchaseFields(StorefrontClient client, int id)
    {
        return new()
        {
            [$"addtocart_{id}.EnteredQuantity"] = "1",
            ["__RequestVerificationToken"] = client.Token
        };
    }

    private static async Task WaitForUiAsync(object fixture)
    {
        var path = Environment.GetEnvironmentVariable("STOREFRONT_TEST_UI_GATE");
        if (string.IsNullOrEmpty(path))
            return;
        path = Path.GetFullPath(path);
        if (!path.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("UI gate must be in local Temp.");
        await File.WriteAllTextAsync(path + ".ready", JsonSerializer.Serialize(fixture));
        var until = DateTime.UtcNow.AddMinutes(8);
        while (!File.Exists(path + ".done"))
        {
            if (DateTime.UtcNow >= until)
                throw new TimeoutException("Product policy UI inspection did not release fixture.");
            await Task.Delay(250);
        }
    }
}