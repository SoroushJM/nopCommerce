using System.Net;
using System.Text.Json;

namespace Nop.Storefront.Tests;

[LocalAdminFixture]
[NotInParallel("storefront-http")]
public sealed class NativeCartSettingsTests
{
    [Test]
    public async Task MiniCartRespectsNativeItemCapImagesEnabledAndCheckoutSettings()
    {
        await using var admin = new StorefrontClient();
        await using var guest = new StorefrontClient();
        await LocalAdminFixture.SignInAsync(admin);
        await admin.InitializeAsync("Admin/Setting/AllSettings");
        var originals = new Dictionary<string, JsonElement>();
        foreach (var name in new[]
        {
            "shoppingcartsettings.minishoppingcartenabled",
            "shoppingcartsettings.minishoppingcartproductnumber",
            "shoppingcartsettings.showproductimagesinminishoppingcart",
            "ordersettings.checkoutdisabled",
            "ordersettings.termsofserviceonshoppingcartpage"
        })
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
            originals.Add(name, rows[0]);
        }
        try
        {
            await NativeCartTests.AddItemAsync(guest);
            await UpdateAsync(admin, originals["shoppingcartsettings.minishoppingcartenabled"], "True");
            await UpdateAsync(admin, originals["shoppingcartsettings.minishoppingcartproductnumber"], "1");
            await UpdateAsync(admin, originals["shoppingcartsettings.showproductimagesinminishoppingcart"], "True");
            await UpdateAsync(admin, originals["ordersettings.checkoutdisabled"], "False");
            await UpdateAsync(admin, originals["ordersettings.termsofserviceonshoppingcartpage"], "False");
            var visible = await guest.NativeCartAsync();
            await Assert.That(visible.GetProperty("MiniEnabled").GetBoolean()).IsTrue();
            await Assert.That(visible.GetProperty("DisplayCheckoutButton").GetBoolean()).IsTrue();
            await Assert.That(visible.GetProperty("Count").GetInt32()).IsEqualTo(1);
            await Assert.That(visible.GetProperty("Lines").GetArrayLength()).IsEqualTo(1);
            await Assert.That(visible.GetProperty("Lines")[0].GetProperty("Image").GetString()!.Length).IsGreaterThan(0);

            await UpdateAsync(admin, originals["shoppingcartsettings.minishoppingcartproductnumber"], "0");
            var capped = await guest.NativeCartAsync();
            await Assert.That(capped.GetProperty("Count").GetInt32()).IsEqualTo(1);
            await Assert.That(capped.GetProperty("Lines").GetArrayLength()).IsEqualTo(0);
            await Assert.That(capped.GetProperty("MiniSubTotal").GetString()).IsEqualTo(visible.GetProperty("MiniSubTotal").GetString());
            await Assert.That((await guest.InitializeAsync()).Contains("data-cart-itemid=", StringComparison.Ordinal)).IsTrue();

            await UpdateAsync(admin, originals["shoppingcartsettings.minishoppingcartproductnumber"], "1");
            await UpdateAsync(admin, originals["shoppingcartsettings.showproductimagesinminishoppingcart"], "False");
            var noImages = await guest.NativeCartAsync();
            await Assert.That(noImages.GetProperty("ShowProductImages").GetBoolean()).IsFalse();
            await Assert.That(noImages.GetProperty("Lines")[0].GetProperty("Image").GetString()).IsEqualTo("");

            await UpdateAsync(admin, originals["shoppingcartsettings.minishoppingcartenabled"], "False");
            await UpdateAsync(admin, originals["ordersettings.checkoutdisabled"], "True");
            var disabled = await guest.NativeCartAsync();
            await Assert.That(disabled.GetProperty("MiniEnabled").GetBoolean()).IsFalse();
            await Assert.That(disabled.GetProperty("DisplayCheckoutButton").GetBoolean()).IsFalse();
            await Assert.That(disabled.GetProperty("Count").GetInt32()).IsEqualTo(1);
            await Assert.That((await guest.InitializeAsync()).Contains("data-cart-itemid=", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            var errors = new List<Exception>();
            foreach (var row in originals.Values)
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
            try
            {
                await guest.ClearCartAsync();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
            if (errors.Count > 0)
                throw new AggregateException("Cart settings or the test cart could not be restored.", errors);
        }
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