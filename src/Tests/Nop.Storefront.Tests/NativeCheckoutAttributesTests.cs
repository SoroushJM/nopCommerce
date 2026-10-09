using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeCheckoutAttributesTests
{
    [Test]
    public async Task DrawerQuantityPreservesNativeCheckboxDateTextFileAndReadonlySelections()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var attributes = new List<int>();
        var prefix = "TUnit checkout " + Guid.NewGuid().ToString("N");
        try
        {
            var checkbox = await CreateAttributeAsync(admin, prefix + " checkbox", 3, attributes);
            var date = await CreateAttributeAsync(admin, prefix + " date", 20, attributes);
            var text = await CreateAttributeAsync(admin, prefix + " note", 10, attributes);
            var file = await CreateAttributeAsync(admin, prefix + " file", 30, attributes);
            var readOnly = await CreateAttributeAsync(admin, prefix + " readonly", 50, attributes);
            var first = await CreateValueAsync(admin, checkbox, "گزینهٔ نخست");
            var second = await CreateValueAsync(admin, checkbox, "گزینهٔ دوم");
            var fixedValue = await CreateValueAsync(admin, readOnly, "انتخاب ثابت", preselected: true);
            var itemId = await NativeCartTests.AddItemAsync(guest);
            var upload = await guest.UploadCheckoutFileAsync(file);
            await Assert.That(upload.GetProperty("success").GetBoolean()).IsTrue();
            var guid = upload.GetProperty("downloadGuid").GetString()!;
            var snapshot = await guest.NativeCartAsync();
            var fields = Form(snapshot, itemId, "1");
            fields.RemoveAll(field => attributes.Any(id => field.Key == "checkout_attribute_" + id || field.Key.StartsWith("checkout_attribute_" + id + "_", StringComparison.Ordinal)));
            fields.AddRange(new KeyValuePair<string, string>[]
            {
                new($"checkout_attribute_{checkbox}", first.ToString()),
                new($"checkout_attribute_{checkbox}", second.ToString()),
                new($"checkout_attribute_{date}_day", "10"),
                new($"checkout_attribute_{date}_month", "10"),
                new($"checkout_attribute_{date}_year", "2026"),
                new($"checkout_attribute_{text}", "برای هدیه بسته‌بندی شود"),
                new($"checkout_attribute_{file}", guid),
                new($"checkout_attribute_{readOnly}", "2147483647")
            });
            using var selected = await guest.PostNativeCartAsync(fields);
            await StorefrontClient.ReadJsonAsync(selected);
            var saved = await guest.NativeCartAsync();
            await VerifyFieldsAsync(saved, checkbox, first, second, date, text, file, guid, readOnly, fixedValue);
            using var quantity = await guest.PostNativeCartAsync(Form(saved, itemId, "2"));
            var result = await StorefrontClient.ReadJsonAsync(quantity);
            await Assert.That(result.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            var again = await guest.NativeCartAsync();
            await VerifyFieldsAsync(again, checkbox, first, second, date, text, file, guid, readOnly, fixedValue);
            var html = WebUtility.HtmlDecode(await guest.InitializeAsync());
            await Assert.That(html.Contains("برای هدیه بسته‌بندی شود", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains(guid, StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            var errors = new List<Exception>();
            try
            {
                await guest.ClearCartAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            foreach (var id in attributes)
            {
                try
                {
                    await admin.InitializeAsync($"Admin/CheckoutAttribute/Edit/{id}");
                    using var deleted = await admin.PostFormAsync($"Admin/CheckoutAttribute/Delete/{id}", new()
                    {
                        ["__RequestVerificationToken"] = admin.Token
                    });
                    await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }
            if (errors.Count > 0)
                throw new AggregateException("Checkout test attributes could not all be removed.", errors);
        }
    }

    private static List<KeyValuePair<string, string>> Form(JsonElement cart, int itemId, string quantity)
    {
        var fields = new List<KeyValuePair<string, string>>();
        foreach (var field in cart.GetProperty("CheckoutFields").EnumerateArray())
        {
            foreach (var value in field.GetProperty("Values").EnumerateArray())
                fields.Add(new(field.GetProperty("Name").GetString()!, value.GetString()!));
        }
        fields.Add(new(cart.GetProperty("TokenField").GetString()!, cart.GetProperty("Token").GetString()!));
        fields.Add(new("updatecart", "1"));
        fields.Add(new($"itemquantity{itemId}", quantity));
        return fields;
    }

    private static async Task VerifyFieldsAsync(JsonElement cart, int checkbox, int first, int second, int date,
        int text, int file, string guid, int readOnly, int fixedValue)
    {
        var fields = cart.GetProperty("CheckoutFields").EnumerateArray().ToDictionary(
            field => field.GetProperty("Name").GetString()!,
            field => field.GetProperty("Values").EnumerateArray().Select(value => value.GetString()!).ToArray());
        await Assert.That(fields[$"checkout_attribute_{checkbox}"]).IsEquivalentTo(new[] { first.ToString(), second.ToString() });
        await Assert.That(fields[$"checkout_attribute_{date}_day"]).IsEquivalentTo(new[] { "10" });
        await Assert.That(fields[$"checkout_attribute_{date}_month"]).IsEquivalentTo(new[] { "10" });
        await Assert.That(fields[$"checkout_attribute_{date}_year"]).IsEquivalentTo(new[] { "2026" });
        await Assert.That(fields[$"checkout_attribute_{text}"]).IsEquivalentTo(new[] { "برای هدیه بسته‌بندی شود" });
        await Assert.That(fields[$"checkout_attribute_{file}"]).IsEquivalentTo(new[] { guid });
        await Assert.That(fields[$"checkout_attribute_{readOnly}"]).IsEquivalentTo(new[] { fixedValue.ToString() });
    }

    private static async Task<int> CreateAttributeAsync(StorefrontClient admin, string name, int type, List<int> created)
    {
        await admin.InitializeAsync("Admin/CheckoutAttribute/Create");
        using var response = await admin.PostFormAsync("Admin/CheckoutAttribute/Create", new()
        {
            ["Name"] = name,
            ["AttributeControlTypeId"] = type.ToString(),
            ["IsTaxExempt"] = "true",
            ["DisplayOrder"] = "100",
            ["ValidationFileAllowedExtensions"] = "txt",
            ["save-continue"] = "true",
            ["__RequestVerificationToken"] = admin.Token
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
        var match = Regex.Match(response.Headers.Location?.OriginalString ?? "", "/Edit/([0-9]+)", RegexOptions.IgnoreCase);
        await Assert.That(match.Success).IsTrue();
        var id = int.Parse(match.Groups[1].Value);
        created.Add(id);
        return id;
    }

    private static async Task<int> CreateValueAsync(StorefrontClient admin, int attributeId, string name, bool preselected = false)
    {
        await admin.InitializeAsync($"Admin/CheckoutAttribute/ValueCreatePopup?checkoutAttributeId={attributeId}");
        using var response = await admin.PostFormAsync("Admin/CheckoutAttribute/ValueCreatePopup", new()
        {
            ["AttributeId"] = attributeId.ToString(),
            ["Name"] = name,
            ["IsPreSelected"] = preselected.ToString(),
            ["__RequestVerificationToken"] = admin.Token
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        using var listed = await admin.PostFormAsync("Admin/CheckoutAttribute/ValueList", new()
        {
            ["CheckoutAttributeId"] = attributeId.ToString(),
            ["Length"] = "100",
            ["__RequestVerificationToken"] = admin.Token
        });
        var values = await StorefrontClient.ReadJsonAsync(listed);
        return values.GetProperty("Data").EnumerateArray().Single(value => value.GetProperty("Name").GetString() == name).GetProperty("Id").GetInt32();
    }
}