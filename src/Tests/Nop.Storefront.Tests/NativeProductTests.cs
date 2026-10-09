using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[NotInParallel("storefront-http")]
public sealed class NativeProductTests
{
    private static string Page => $"product/productdetails?productId={StorefrontClient.ProductId}";
    private static string Add => $"addproducttocart/details/{StorefrontClient.ProductId}/1";

    [Test]
    public async Task ProductIsPrerenderedWithClientBootAndAntiforgery()
    {
        await using var client = new StorefrontClient();
        var html = await client.InitializeAsync(Page);
        await Assert.That(html.Contains("\"type\":\"webassembly\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("\"type\":\"server\"", StringComparison.Ordinal)).IsFalse();
        await Assert.That(html.Contains("/_framework/blazor.webassembly.js", StringComparison.Ordinal)).IsTrue();
        await Assert.That(Regex.Matches(html, "<!DOCTYPE html>", RegexOptions.IgnoreCase).Count).IsEqualTo(1);
        await Assert.That(client.Token.Length > 20).IsTrue();
        using var boot = await client.GetAsync("_framework/blazor.webassembly.js");
        await Assert.That(boot.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task NativeSelectionReturnsServerFormattedPrice()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync(Page);
        var (available, unavailable) = await Combinations(client);
        await Assert.That(unavailable.GetProperty("InStock").GetBoolean()).IsFalse();
        using var response = await client.PostFormAsync($"shoppingcart/productdetails_attributechange/{StorefrontClient.ProductId}/true/true", Form(client, available));
        var quote = await StorefrontClient.ReadJsonAsync(response);
        await Assert.That(!string.IsNullOrWhiteSpace(quote.GetProperty("price").GetString())).IsTrue();
    }

    [Test]
    public async Task NativeCartRejectsMissingAntiforgeryAndUnavailableSelection()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync(Page);
        var (available, unavailable) = await Combinations(client);
        try
        {
            using var csrf = await client.PostFormAsync(Add, Form(client, available, includeToken: false));
            await Assert.That(csrf.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            using var response = await client.PostFormAsync(Add, Form(client, unavailable));
            var result = await StorefrontClient.ReadJsonAsync(response);
            await Assert.That(result.GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That((await client.CartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(0);
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    [Test]
    public async Task NativeAddCreatesPersistentGuestCart()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync(Page);
        var (available, _) = await Combinations(client);
        try
        {
            using var response = await client.PostFormAsync(Add, Form(client, available));
            var result = await StorefrontClient.ReadJsonAsync(response);
            await Assert.That(result.GetProperty("success").GetBoolean()).IsTrue();
            var cart = await client.CartAsync();
            await Assert.That(cart.GetProperty("SignedIn").GetBoolean()).IsFalse();
            await Assert.That(cart.GetProperty("Lines").GetArrayLength()).IsEqualTo(1);
            await Assert.That(cart.GetProperty("Lines")[0].GetProperty("Quantity").GetInt32()).IsEqualTo(1);
            var again = await client.CartAsync();
            await Assert.That(again.GetProperty("Lines")[0].GetProperty("Id").GetInt32()).IsEqualTo(cart.GetProperty("Lines")[0].GetProperty("Id").GetInt32());
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    private static async Task<(JsonElement Available, JsonElement Unavailable)> Combinations(StorefrontClient client)
    {
        var combinations = await client.GetJsonAsync($"product/combinations?productId={StorefrontClient.ProductId}");
        return (combinations.EnumerateArray().First(item => item.GetProperty("InStock").GetBoolean()), combinations.EnumerateArray().First(item => !item.GetProperty("InStock").GetBoolean()));
    }

    [Test]
    public async Task NativeEditRestoresAttributesAndMergesMatchingSelection()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync(Page);
        var combinations = await client.GetJsonAsync($"product/combinations?productId={StorefrontClient.ProductId}");
        var available = combinations.EnumerateArray().Where(item => item.GetProperty("InStock").GetBoolean()).Take(2).ToArray();
        await Assert.That(available.Length).IsEqualTo(2);
        try
        {
            using var first = await client.PostFormAsync(Add, Form(client, available[0]));
            await Assert.That((await StorefrontClient.ReadJsonAsync(first)).GetProperty("success").GetBoolean()).IsTrue();
            var firstId = (await client.NativeCartAsync()).GetProperty("Lines")[0].GetProperty("Id").GetInt32();
            using var second = await client.PostFormAsync(Add, Form(client, available[1]));
            await Assert.That((await StorefrontClient.ReadJsonAsync(second)).GetProperty("success").GetBoolean()).IsTrue();
            await Assert.That((await client.NativeCartAsync()).GetProperty("Lines").GetArrayLength()).IsEqualTo(2);
            var edit = await client.InitializeAsync(Page + "&updatecartitemid=" + firstId);
            foreach (var attribute in available[0].GetProperty("Attributes").EnumerateArray())
            {
                var mappingId = attribute.GetProperty("Id").GetInt32();
                var valueId = attribute.GetProperty("ValueIds")[0].GetInt32();
                await Assert.That(edit.Contains($"name=\"product_attribute_{mappingId}\" value=\"{valueId}\"", StringComparison.Ordinal)).IsTrue();
            }
            var fields = Form(client, available[1]);
            fields[$"addtocart_{StorefrontClient.ProductId}.EnteredQuantity"] = "2";
            fields[$"addtocart_{StorefrontClient.ProductId}.UpdatedShoppingCartItemId"] = firstId.ToString();
            using var updated = await client.PostFormAsync(Add, fields);
            await Assert.That((await StorefrontClient.ReadJsonAsync(updated)).GetProperty("success").GetBoolean()).IsTrue();
            var merged = (await client.NativeCartAsync()).GetProperty("Lines");
            await Assert.That(merged.GetArrayLength()).IsEqualTo(1);
            await Assert.That(merged[0].GetProperty("Id").GetInt32()).IsEqualTo(firstId);
            await Assert.That(merged[0].GetProperty("Quantity").GetInt32()).IsEqualTo(3);
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    private static Dictionary<string, string> Form(StorefrontClient client, JsonElement combination, bool includeToken = true)
    {
        var fields = new Dictionary<string, string>
        {
            [$"addtocart_{StorefrontClient.ProductId}.EnteredQuantity"] = "1"
        };
        foreach (var attribute in combination.GetProperty("Attributes").EnumerateArray())
            fields[$"product_attribute_{attribute.GetProperty("Id").GetInt32()}"] = attribute.GetProperty("ValueIds")[0].GetInt32().ToString();
        if (includeToken)
            fields["__RequestVerificationToken"] = client.Token;
        return fields;
    }
}