using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

[NotInParallel("storefront-http")]
public sealed class StorefrontApiTests
{
    [Test]
    public async Task NativeVariantPriceAndStockMatchPersistentGuestCart()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync($"product/productdetails?productId={StorefrontClient.ProductId}");
        var combinations = await client.GetJsonAsync($"product/combinations?productId={StorefrontClient.ProductId}");
        var available = combinations.EnumerateArray().First(item => item.GetProperty("InStock").GetBoolean());
        var unavailable = combinations.EnumerateArray().First(item => !item.GetProperty("InStock").GetBoolean());
        using var quoted = await client.PostFormAsync($"shoppingcart/productdetails_attributechange/{StorefrontClient.ProductId}/true/true", Form(client, available));
        var quote = await StorefrontClient.ReadJsonAsync(quoted);
        var price = quote.GetProperty("price").GetString();
        await Assert.That(!string.IsNullOrWhiteSpace(price)).IsTrue();
        try
        {
            using var added = await client.PostFormAsync(Add, Form(client, available));
            await Assert.That((await StorefrontClient.ReadJsonAsync(added)).GetProperty("success").GetBoolean()).IsTrue();
            var cart = await client.NativeCartAsync();
            await Assert.That(cart.GetProperty("Count").GetInt32()).IsEqualTo(1);
            await Assert.That(cart.GetProperty("SignedIn").GetBoolean()).IsFalse();
            await Assert.That(cart.GetProperty("Lines")[0].GetProperty("UnitPrice").GetString()).IsEqualTo(price);
            using var rejected = await client.PostFormAsync(Add, Form(client, unavailable));
            await Assert.That((await StorefrontClient.ReadJsonAsync(rejected)).GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That((await client.NativeCartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(1);
        }
        finally
        {
            await client.ClearCartAsync();
        }
    }

    [Test]
    public async Task OtpMigratesAndMergesGuestCartWhileProtectingOwnership()
    {
        await using var first = new StorefrontClient();
        await using var second = new StorefrontClient();
        await using var outsider = new StorefrontClient();
        await first.InitializeAsync();
        await second.InitializeAsync();
        await outsider.InitializeAsync();
        var combinations = await first.GetJsonAsync($"product/combinations?productId={StorefrontClient.ProductId}");
        var selection = combinations.EnumerateArray().First(item => item.GetProperty("InStock").GetBoolean());
        var phone = "0900" + RandomNumberGenerator.GetInt32(1000000, 10000000);
        try
        {
            using var firstAdd = await first.PostFormAsync(Add, Form(first, selection));
            await Assert.That((await StorefrontClient.ReadJsonAsync(firstAdd)).GetProperty("success").GetBoolean()).IsTrue();
            await Login(first, phone);
            var cart = await first.NativeCartAsync();
            await Assert.That(cart.GetProperty("SignedIn").GetBoolean()).IsTrue();
            await Assert.That(cart.GetProperty("Count").GetInt32()).IsEqualTo(1);
            using var secondAdd = await second.PostFormAsync(Add, Form(second, selection));
            await Assert.That((await StorefrontClient.ReadJsonAsync(secondAdd)).GetProperty("success").GetBoolean()).IsTrue();
            await Login(second, phone);
            var merged = await second.NativeCartAsync();
            await Assert.That(merged.GetProperty("SignedIn").GetBoolean()).IsTrue();
            await Assert.That(merged.GetProperty("Count").GetInt32()).IsEqualTo(2);
            var lineId = merged.GetProperty("Lines")[0].GetProperty("Id").GetInt32();
            var crossCustomer = await outsider.UpdateNativeQuantityAsync(lineId, 0);
            await Assert.That(crossCustomer.GetProperty("Count").GetInt32()).IsEqualTo(0);
            await Assert.That((await second.NativeCartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(2);
            var changed = await second.UpdateNativeQuantityAsync(lineId, 3);
            await Assert.That(changed.GetProperty("Count").GetInt32()).IsEqualTo(3);
            await Assert.That(changed.GetProperty("Lines")[0].GetProperty("UnitPrice").GetString())
                .IsEqualTo(merged.GetProperty("Lines")[0].GetProperty("UnitPrice").GetString());
            var removed = await second.UpdateNativeQuantityAsync(lineId, 0);
            await Assert.That(removed.GetProperty("Count").GetInt32()).IsEqualTo(0);
            using var csrf = await outsider.PostFormAsync(Add, Form(outsider, selection, includeToken: false));
            await Assert.That(csrf.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That((await outsider.NativeCartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(0);
        }
        finally
        {
            try
            {
                await first.InitializeAsync();
                await first.ClearCartAsync();
            }
            finally
            {
                try
                {
                    await second.InitializeAsync();
                    await second.ClearCartAsync();
                }
                finally
                {
                    await outsider.ClearCartAsync();
                }
            }
        }
    }

    [Test]
    public async Task DevelopmentOpenApiDescribesStorefrontEndpoints()
    {
        await using var client = new StorefrontClient();
        var document = await client.GetJsonAsync("stationery/openapi/v1.json");
        var paths = document.GetProperty("paths").EnumerateObject().Select(path => path.Name).Order().ToArray();
        await Assert.That(paths).IsEquivalentTo(new[] { "/stationery/api/otp/send", "/stationery/api/otp/verify" });
    }

    [Test]
    [Arguments("stationery/swagger", "/stationery/swagger/index.html", HttpStatusCode.MovedPermanently)]
    [Arguments("stationery/scalar", "/stationery/scalar/", HttpStatusCode.Found)]
    public async Task DevelopmentApiExplorerServesHtml(string path, string canonicalPath, HttpStatusCode redirectStatus)
    {
        await using var client = new StorefrontClient();
        using var entry = await client.GetAsync(path);
        await Assert.That(entry.StatusCode).IsEqualTo(redirectStatus);
        var location = entry.Headers.Location!;
        var actualPath = location.IsAbsoluteUri ? location.AbsolutePath : new Uri(new Uri("http://localhost/" + path), location).AbsolutePath;
        await Assert.That(actualPath).IsEqualTo(canonicalPath);
        using var response = await client.GetAsync(canonicalPath);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType?.MediaType).IsEqualTo("text/html");
    }

    private static async Task Login(StorefrontClient client, string phone)
    {
        var sent = await client.PostJsonAsync("stationery/api/otp/send", new
        {
            phone
        });
        await Assert.That(sent.GetProperty("Success").GetBoolean()).IsTrue();
        var code = sent.GetProperty("TestCode").GetString();
        await Assert.That(code?.Length).IsEqualTo(6);
        var again = await client.PostJsonAsync("stationery/api/otp/send", new
        {
            phone
        });
        await Assert.That(again.GetProperty("Success").GetBoolean()).IsFalse();
        var wrong = await client.PostJsonAsync("stationery/api/otp/verify", new
        {
            phone,
            code = "000000"
        });
        await Assert.That(wrong.GetProperty("Success").GetBoolean()).IsFalse();
        var accepted = await client.PostJsonAsync("stationery/api/otp/verify", new
        {
            phone,
            code
        });
        await Assert.That(accepted.GetProperty("Success").GetBoolean()).IsTrue();
        await client.InitializeAsync();
        var replay = await client.PostJsonAsync("stationery/api/otp/verify", new
        {
            phone,
            code
        });
        await Assert.That(replay.GetProperty("Success").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task DirectLoginUsesNativeLayoutAndClientPrerendering()
    {
        await using var client = new StorefrontClient();
        var html = await client.InitializeAsync("stationery/login");
        await Assert.That(html.Contains("\"type\":\"webassembly\"", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("\"type\":\"server\"", StringComparison.Ordinal)).IsFalse();
        var openLogin = false;
        foreach (Match match in Regex.Matches(html, "<!--Blazor:(.*?)-->", RegexOptions.Singleline))
        {
            using var marker = JsonDocument.Parse(match.Groups[1].Value);
            if (!marker.RootElement.TryGetProperty("typeName", out var type) || type.GetString() != "Storefront.UI.Components.NativeHeader")
                continue;
            using var parameters = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker.RootElement.GetProperty("parameterValues").GetString()!)));
            openLogin = parameters.RootElement.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.True);
        }
        await Assert.That(openLogin).IsTrue();
        await Assert.That(html.Contains("blazor.server.js", StringComparison.Ordinal)).IsFalse();
        using var oldHub = await client.GetAsync("_blazor/negotiate");
        await Assert.That(oldHub.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RemovedCustomCommerceEndpointsAreUnavailable()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync();
        foreach (var path in new[] { "stationery/api/catalog", "stationery/api/cart", "PersianStorefront/Index" })
        {
            using var response = await client.GetAsync(path);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
        foreach (var path in new[] { "quote", "cart/add", "cart/quantity" })
        {
            using var response = await client.PostAsync("stationery/api/" + path, new
            {
            });
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    private static string Add => $"addproducttocart/details/{StorefrontClient.ProductId}/1";

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