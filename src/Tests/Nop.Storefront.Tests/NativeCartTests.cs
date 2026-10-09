using System.Net;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[NotInParallel("storefront-http")]
public sealed class NativeCartTests
{
    [Test]
    public async Task NativeCartUpdatesValidatesAndRemovesItsOwnItem()
    {
        await using var client = new StorefrontClient();
        var id = await AddItemAsync(client);
        try
        {
            using var update = await client.PostFormAsync("cart", Fields(client, id, "2"));
            var html = await update.Content.ReadAsStringAsync();
            await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(Quantity(html, id)).IsEqualTo(2);
            await Assert.That(html.Contains("class=\"total-info\"", StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains($"data-cart-subtotal=\"{id}\"", StringComparison.Ordinal)).IsTrue();

            using var invalid = await client.PostFormAsync("cart", Fields(client, id, "999999"));
            var warningHtml = await invalid.Content.ReadAsStringAsync();
            await Assert.That(invalid.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(Quantity(warningHtml, id)).IsEqualTo(2);
            await Assert.That(warningHtml.Contains("role=\"alert\"", StringComparison.Ordinal)).IsTrue();
            var again = await client.InitializeAsync();
            await Assert.That(Quantity(again, id)).IsEqualTo(2);

            using var removed = await client.PostFormAsync("cart", new()
            {
                ["updatecart"] = "1",
                ["removefromcart"] = id.ToString(),
                ["__RequestVerificationToken"] = client.Token
            });
            await Assert.That(removed.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That((await removed.Content.ReadAsStringAsync()).Contains("data-cart-itemid=", StringComparison.Ordinal)).IsFalse();
            using var legacy = await client.GetAsync("stationery/cart");
            await Assert.That(legacy.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(legacy.Headers.Location?.OriginalString).IsEqualTo("/cart");
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    [Test]
    public async Task NativeCartRequiresCsrfAndIgnoresAnotherCustomersItem()
    {
        await using var owner = new StorefrontClient();
        await using var other = new StorefrontClient();
        var id = await AddItemAsync(owner);
        try
        {
            var fields = Fields(owner, id, "2");
            fields.Remove("__RequestVerificationToken");
            using var missingToken = await owner.PostFormAsync("cart", fields);
            await Assert.That(missingToken.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await other.InitializeAsync();
            using var forged = await other.PostFormAsync("cart", Fields(other, id, "3"));
            await Assert.That(forged.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That((await forged.Content.ReadAsStringAsync()).Contains("data-cart-itemid=", StringComparison.Ordinal)).IsFalse();
            await Assert.That(Quantity(await owner.InitializeAsync(), id)).IsEqualTo(1);
        }
        finally
        {
            await owner.ClearCartAsync();
            await other.ClearCartAsync();
        }
    }

    [Test]
    public async Task NativeDrawerUsesFormattedCartValuesAndPreservesAttemptWarnings()
    {
        await using var client = new StorefrontClient();
        var id = await AddItemAsync(client);
        try
        {
            var cart = await client.NativeCartAsync();
            var line = cart.GetProperty("Lines").EnumerateArray().Single(item => item.GetProperty("Id").GetInt32() == id);
            var html = WebUtility.HtmlDecode(await client.InitializeAsync());
            await Assert.That(html.Contains(line.GetProperty("SubTotal").GetString()!, StringComparison.Ordinal)).IsTrue();
            await Assert.That(html.Contains(line.GetProperty("UnitPrice").GetString()!, StringComparison.Ordinal)).IsTrue();
            foreach (var row in cart.GetProperty("Totals").EnumerateArray())
                await Assert.That(html.Contains(row.GetProperty("Value").GetString()!, StringComparison.Ordinal)).IsTrue();
            using var updated = await client.PostNativeCartAsync(Fields(client, id, "2"));
            var result = await StorefrontClient.ReadJsonAsync(updated);
            await Assert.That(updated.Headers.CacheControl?.NoStore == true).IsTrue();
            await Assert.That(result.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            using var invalid = await client.PostNativeCartAsync(Fields(client, id, "999999"));
            var warning = await StorefrontClient.ReadJsonAsync(invalid);
            await Assert.That(warning.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await Assert.That(warning.GetProperty("ItemWarnings").GetArrayLength()).IsEqualTo(1);
            await Assert.That(warning.GetProperty("ItemWarnings")[0].GetProperty("Messages").GetArrayLength() > 0).IsTrue();
            var again = await client.NativeCartAsync();
            await Assert.That(again.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(2);
            await Assert.That(again.GetProperty("ItemWarnings").GetArrayLength()).IsEqualTo(0);
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    [Test]
    public async Task DrawerAdapterLeavesCouponActionNativeAndRequiresCsrf()
    {
        await using var client = new StorefrontClient();
        var id = await AddItemAsync(client);
        try
        {
            await client.NativeCartAsync();
            var fields = Fields(client, id, "2");
            fields.Remove("__RequestVerificationToken");
            using var missing = await client.PostNativeCartAsync(fields);
            await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            using var coupon = await client.PostNativeCartAsync(new Dictionary<string, string>
            {
                ["applydiscountcouponcode"] = "1",
                ["discountcouponcode"] = "missing-test-" + Guid.NewGuid().ToString("N"),
                ["__RequestVerificationToken"] = client.Token
            });
            await Assert.That(coupon.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(coupon.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
            var unchanged = await client.NativeCartAsync();
            await Assert.That(unchanged.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(1);
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    internal static async Task<int> AddItemAsync(StorefrontClient client)
    {
        await client.InitializeAsync($"product/productdetails?productId={StorefrontClient.ProductId}");
        var combinations = await client.GetJsonAsync($"product/combinations?productId={StorefrontClient.ProductId}");
        var available = combinations.EnumerateArray().First(item => item.GetProperty("InStock").GetBoolean());
        var fields = new Dictionary<string, string>
        {
            [$"addtocart_{StorefrontClient.ProductId}.EnteredQuantity"] = "1",
            ["__RequestVerificationToken"] = client.Token
        };
        foreach (var attribute in available.GetProperty("Attributes").EnumerateArray())
            fields[$"product_attribute_{attribute.GetProperty("Id").GetInt32()}"] = attribute.GetProperty("ValueIds")[0].GetInt32().ToString();
        using var added = await client.PostFormAsync($"addproducttocart/details/{StorefrontClient.ProductId}/1", fields);
        var result = await StorefrontClient.ReadJsonAsync(added);
        await Assert.That(result.GetProperty("success").GetBoolean()).IsTrue();
        try
        {
            var html = await client.InitializeAsync();
            return int.Parse(Regex.Match(html, "data-cart-itemid=\"([0-9]+)\"").Groups[1].Value);
        }
        catch
        {
            await client.ClearCartAsync();
            throw;
        }
    }

    private static Dictionary<string, string> Fields(StorefrontClient client, int id, string quantity)
    {
        return new()
        {
            ["updatecart"] = "1",
            [$"itemquantity{id}"] = quantity,
            ["__RequestVerificationToken"] = client.Token
        };
    }

    private static int Quantity(string html, int id)
    {
        return int.Parse(Regex.Match(html, $"name=\"itemquantity{id}\"[^>]*value=\"([0-9]+)\"").Groups[1].Value);
    }
}