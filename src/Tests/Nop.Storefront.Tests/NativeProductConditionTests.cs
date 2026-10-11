using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeProductConditionTests
{
    [Test]
    public async Task InitialConditionsAndCartRestorationFollowNativeAttributeParsing()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var productId = 0;
        var attributeIds = new List<int>();
        var mappingIds = new List<int>();
        var errors = new List<Exception>();
        var childName = "گزینه وابسته " + Guid.NewGuid().ToString("N");
        try
        {
            await admin.InitializeAsync("Admin/Product/Create");
            using (var created = await admin.PostFormAsync("Admin/Product/Create", new()
            {
                ["Name"] = "آزمون شرط ویژگی " + Guid.NewGuid().ToString("N"),
                ["ProductTypeId"] = "5",
                ["ProductTemplateId"] = "1",
                ["Published"] = "true",
                ["VisibleIndividually"] = "true",
                ["Price"] = "100000",
                ["ManageInventoryMethodId"] = "0",
                ["OrderMinimumQuantity"] = "1",
                ["OrderMaximumQuantity"] = "10",
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            }))
                productId = await RedirectIdAsync(created, "Edit");

            foreach (var name in new[] { "انتخاب اصلی " + Guid.NewGuid().ToString("N"), childName })
            {
                await admin.InitializeAsync("Admin/ProductAttribute/Create");
                using (var created = await admin.PostFormAsync("Admin/ProductAttribute/Create", new()
                {
                    ["Name"] = name,
                    ["save-continue"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                }))
                    attributeIds.Add(await RedirectIdAsync(created, "Edit"));
                await admin.InitializeAsync($"Admin/Product/ProductAttributeMappingCreate?productId={productId}");
                using var mapped = await admin.PostFormAsync("Admin/Product/ProductAttributeMappingCreate", new()
                {
                    ["ProductId"] = productId.ToString(),
                    ["ProductAttributeId"] = attributeIds[^1].ToString(),
                    ["AttributeControlTypeId"] = "1",
                    ["IsRequired"] = "true",
                    ["save-continue"] = "true",
                    ["__RequestVerificationToken"] = admin.Token
                });
                mappingIds.Add(await RedirectIdAsync(mapped, "ProductAttributeMappingEdit"));
            }
            var parent = mappingIds[0];
            var child = mappingIds[1];
            var off = await CreateValueAsync(admin, parent, "بدون گزینه وابسته", true, "0");
            var on = await CreateValueAsync(admin, parent, "با گزینه وابسته", false, "0");
            var dependent = await CreateValueAsync(admin, child, "انتخاب وابسته", true, "10000");
            await admin.InitializeAsync($"Admin/Product/ProductAttributeMappingEdit/{child}");
            using (var saved = await admin.PostFormAsync($"Admin/Product/ProductAttributeMappingEdit/{child}", new()
            {
                ["Id"] = child.ToString(),
                ["ProductId"] = productId.ToString(),
                ["ProductAttributeId"] = attributeIds[1].ToString(),
                ["AttributeControlTypeId"] = "1",
                ["IsRequired"] = "true",
                ["ConditionModel.EnableCondition"] = "true",
                ["ConditionModel.SelectedProductAttributeId"] = parent.ToString(),
                [$"product_attribute_{parent}"] = on.ToString(),
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            }))
                await Assert.That(await RedirectIdAsync(saved, "ProductAttributeMappingEdit")).IsEqualTo(child);

            var page = $"product/productdetails?productId={productId}";
            var html = WebUtility.HtmlDecode(await guest.InitializeAsync(page));
            await Assert.That(html.Contains(childName + ":", StringComparison.Ordinal)).IsFalse();
            await Assert.That(html.Contains($"name=\"product_attribute_{child}\" value=\"{dependent}\"", StringComparison.Ordinal)).IsTrue();
            // Hidden selections are submitted just like the native form; the server strips them when unmet.
            var offFields = Fields(guest, productId, parent, off, child, dependent);
            using (var quote = await guest.PostFormAsync($"shoppingcart/productdetails_attributechange/{productId}/true/true", offFields))
                await Assert.That((await StorefrontClient.ReadJsonAsync(quote)).GetProperty("disabledattributemappingids")
                    .EnumerateArray().Select(id => id.GetInt32())).Contains(child);
            using (var added = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", offFields))
                await Assert.That((await StorefrontClient.ReadJsonAsync(added)).GetProperty("success").GetBoolean()).IsTrue();
            var offItem = (await guest.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Id").GetInt32();
            var offEdit = WebUtility.HtmlDecode(await guest.InitializeAsync(page + "&updatecartitemid=" + offItem));
            await Assert.That(offEdit.Contains(childName + ":", StringComparison.Ordinal)).IsFalse();
            await Assert.That(offEdit.Contains($"name=\"product_attribute_{child}\"", StringComparison.Ordinal)).IsFalse();
            await guest.ClearCartAsync();

            await guest.InitializeAsync(page);
            var onFields = Fields(guest, productId, parent, on, child, dependent);
            using (var quote = await guest.PostFormAsync($"shoppingcart/productdetails_attributechange/{productId}/true/true", onFields))
                await Assert.That((await StorefrontClient.ReadJsonAsync(quote)).GetProperty("enabledattributemappingids")
                    .EnumerateArray().Select(id => id.GetInt32())).Contains(child);
            using (var added = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", onFields))
                await Assert.That((await StorefrontClient.ReadJsonAsync(added)).GetProperty("success").GetBoolean()).IsTrue();
            var onItem = (await guest.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Id").GetInt32();
            var restored = WebUtility.HtmlDecode(await guest.InitializeAsync(page + "&updatecartitemid=" + onItem));
            await Assert.That(restored.Contains(childName + ":", StringComparison.Ordinal)).IsTrue();
            await Assert.That(restored.Contains($"name=\"product_attribute_{parent}\" value=\"{on}\"", StringComparison.Ordinal)).IsTrue();
        }
        catch (Exception error)
        {
            errors.Add(error);
        }
        finally
        {
            await CleanupAsync(errors, guest.ClearCartAsync);
            foreach (var mapping in mappingIds.AsEnumerable().Reverse())
                await CleanupAsync(errors, async () =>
                {
                    using var deleted = await admin.PostFormAsync("Admin/Product/ProductAttributeMappingDelete", new()
                    {
                        ["id"] = mapping.ToString(),
                        ["__RequestVerificationToken"] = admin.Token
                    });
                    await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
                });
            if (productId > 0)
                await CleanupAsync(errors, () => DeleteAsync(admin, "Product", productId));
            foreach (var attribute in attributeIds)
                await CleanupAsync(errors, () => DeleteAsync(admin, "ProductAttribute", attribute));
        }
        if (errors.Count > 0)
            throw new AggregateException(errors);
    }

    private static Dictionary<string, string> Fields(StorefrontClient guest, int product, int parent, int parentValue, int child, int childValue)
    {
        return new()
        {
            [$"product_attribute_{parent}"] = parentValue.ToString(),
            [$"product_attribute_{child}"] = childValue.ToString(),
            [$"addtocart_{product}.EnteredQuantity"] = "1",
            ["__RequestVerificationToken"] = guest.Token
        };
    }

    private static async Task<int> CreateValueAsync(StorefrontClient admin, int mapping, string name, bool selected, string adjustment)
    {
        var path = $"Admin/Product/ProductAttributeValueCreatePopup?productAttributeMappingId={mapping}";
        await admin.InitializeAsync(path);
        using var created = await admin.PostFormAsync(path, new()
        {
            ["ProductAttributeMappingId"] = mapping.ToString(),
            ["AttributeValueTypeId"] = "0",
            ["Name"] = name,
            ["Quantity"] = "1",
            ["IsPreSelected"] = selected.ToString(),
            ["PriceAdjustment"] = adjustment,
            ["save"] = "true",
            ["__RequestVerificationToken"] = admin.Token
        });
        created.EnsureSuccessStatusCode();
        await Assert.That((await created.Content.ReadAsStringAsync()).Contains("window.close()", StringComparison.Ordinal)).IsTrue();
        using var values = await admin.PostFormAsync("Admin/Product/ProductAttributeValueList", new()
        {
            ["ProductAttributeMappingId"] = mapping.ToString(),
            ["Length"] = "100",
            ["__RequestVerificationToken"] = admin.Token
        });
        return (await StorefrontClient.ReadJsonAsync(values)).GetProperty("Data").EnumerateArray()
            .Single(value => value.GetProperty("Name").GetString() == name).GetProperty("Id").GetInt32();
    }

    private static async Task<int> RedirectIdAsync(HttpResponseMessage response, string action)
    {
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var match = Regex.Match(response.Headers.Location!.OriginalString, "/" + action + "/([0-9]+)", RegexOptions.IgnoreCase);
        await Assert.That(match.Success).IsTrue();
        return int.Parse(match.Groups[1].Value);
    }

    private static async Task DeleteAsync(StorefrontClient admin, string controller, int id)
    {
        await admin.InitializeAsync($"Admin/{controller}/Edit/{id}");
        using var deleted = await admin.PostFormAsync($"Admin/{controller}/Delete/{id}", new()
        {
            ["id"] = id.ToString(),
            ["__RequestVerificationToken"] = admin.Token
        });
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
    }

    private static async Task CleanupAsync(List<Exception> errors, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception error) { errors.Add(error); }
    }
}