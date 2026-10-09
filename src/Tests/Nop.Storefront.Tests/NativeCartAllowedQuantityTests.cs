using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeCartAllowedQuantityTests
{
    [Test]
    public async Task RejectedListedQuantityKeepsSavedQuantityAndAmounts()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        var productId = 0;
        try
        {
            await admin.InitializeAsync("Admin/Product/Create");
            using var created = await admin.PostFormAsync("Admin/Product/Create", new()
            {
                ["Name"] = "آزمون تعداد مجاز " + Guid.NewGuid().ToString("N"),
                ["ProductTypeId"] = "5",
                ["ProductTemplateId"] = "1",
                ["VisibleIndividually"] = "true",
                ["Published"] = "true",
                ["Price"] = "100000",
                ["ManageInventoryMethodId"] = "1",
                ["StockQuantity"] = "3",
                ["OrderMinimumQuantity"] = "2",
                ["OrderMaximumQuantity"] = "4",
                ["AllowedQuantities"] = "2,4",
                ["save-continue"] = "true",
                ["__RequestVerificationToken"] = admin.Token
            });
            await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            var match = Regex.Match(created.Headers.Location?.OriginalString ?? "", "/Edit/([0-9]+)", RegexOptions.IgnoreCase);
            await Assert.That(match.Success).IsTrue();
            productId = int.Parse(match.Groups[1].Value);
            await guest.InitializeAsync($"product/productdetails?productId={productId}");
            using var added = await guest.PostFormAsync($"addproducttocart/details/{productId}/1", new()
            {
                [$"addtocart_{productId}.EnteredQuantity"] = "2",
                ["__RequestVerificationToken"] = guest.Token
            });
            await Assert.That((await StorefrontClient.ReadJsonAsync(added)).GetProperty("success").GetBoolean()).IsTrue();
            var before = await guest.NativeCartAsync();
            var line = before.GetProperty("Lines")[0];
            await Assert.That(line.GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await Assert.That(line.GetProperty("AllowedQuantities").EnumerateArray().Select(option => option.GetProperty("Value").GetString()!)).IsEquivalentTo(new[] { "2", "4" });
            using var changed = await guest.PostNativeCartAsync(new[]
            {
                new KeyValuePair<string, string>(before.GetProperty("TokenField").GetString()!, guest.Token),
                new("updatecart", "1"),
                new("itemquantity" + line.GetProperty("Id").GetInt32(), "4")
            });
            var result = await StorefrontClient.ReadJsonAsync(changed);
            var retained = result.GetProperty("Lines")[0];
            await Assert.That(retained.GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await Assert.That(retained.GetProperty("SubTotal").GetString()).IsEqualTo(line.GetProperty("SubTotal").GetString());
            await Assert.That(result.GetProperty("ItemWarnings").EnumerateArray().Any(warning => warning.GetProperty("Messages").GetArrayLength() > 0)).IsTrue();
            var selected = retained.GetProperty("AllowedQuantities").EnumerateArray().Single(option => option.GetProperty("Selected").GetBoolean());
            await Assert.That(selected.GetProperty("Value").GetString()).IsEqualTo("2");
            await Assert.That((await guest.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await WaitForOptionalUiInspectionAsync(productId);
        }
        finally
        {
            try
            {
                await guest.ClearCartAsync();
            }
            finally
            {
                if (productId > 0)
                {
                    await admin.InitializeAsync($"Admin/Product/Edit/{productId}");
                    using var deleted = await admin.PostFormAsync($"Admin/Product/Delete/{productId}", new()
                    {
                        ["__RequestVerificationToken"] = admin.Token
                    });
                    await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
                }
            }
        }
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
                throw new TimeoutException("The UI inspection did not release its temporary product fixture.");
            await Task.Delay(250);
        }
    }
}