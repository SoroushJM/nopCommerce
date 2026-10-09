using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace Nop.Storefront.Tests;

[NotInParallel("storefront-http")]
public sealed class StorefrontApiTests
{
    [Test]
    public async Task VariantPriceStockAndGuestCartUseRealStoreData()
    {
        await using var client = new StorefrontClient();
        await client.InitializeAsync();
        var (product, attribute, green, orange) = await Fixture(client);
        var selection = Selection(product, attribute, green);
        var quote = await client.PostJsonAsync("stationery/api/quote", selection);
        await Assert.That(quote.GetProperty("Available").GetBoolean()).IsTrue();
        await Assert.That(quote.GetProperty("Price").GetDecimal()).IsEqualTo(product.GetProperty("Price").GetDecimal() + green.GetProperty("Adjustment").GetDecimal());
        try
        {
            var cart = await client.PostJsonAsync("stationery/api/cart/add", selection);
            await Assert.That(cart.GetProperty("Count").GetInt32()).IsEqualTo(1);
            await Assert.That(cart.GetProperty("SignedIn").GetBoolean()).IsFalse();
            var unavailable = Selection(product, attribute, orange);
            var unavailableQuote = await client.PostJsonAsync("stationery/api/quote", unavailable);
            await Assert.That(unavailableQuote.GetProperty("Available").GetBoolean()).IsFalse();
            using var rejected = await client.PostAsync("stationery/api/cart/add", unavailable);
            await Assert.That(rejected.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That((await client.CartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(1);
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
        var (product, attribute, green, _) = await Fixture(first);
        var selection = Selection(product, attribute, green);
        var phone = "0900" + RandomNumberGenerator.GetInt32(1000000, 10000000);
        try
        {
            await first.PostJsonAsync("stationery/api/cart/add", selection);
            await Login(first, phone);
            var cart = await first.CartAsync();
            await Assert.That(cart.GetProperty("SignedIn").GetBoolean()).IsTrue();
            await Assert.That(cart.GetProperty("Count").GetInt32()).IsEqualTo(1);
            await second.PostJsonAsync("stationery/api/cart/add", selection);
            await Login(second, phone);
            var merged = await second.CartAsync();
            await Assert.That(merged.GetProperty("SignedIn").GetBoolean()).IsTrue();
            await Assert.That(merged.GetProperty("Count").GetInt32()).IsEqualTo(2);
            var lineId = merged.GetProperty("Lines")[0].GetProperty("Id").GetInt32();
            using var crossCustomer = await outsider.PostAsync("stationery/api/cart/quantity", new
            {
                lineId,
                quantity = 0
            });
            await Assert.That(crossCustomer.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That((await second.CartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(2);
            var changed = await second.PostJsonAsync("stationery/api/cart/quantity", new
            {
                lineId,
                quantity = 3
            });
            await Assert.That(changed.GetProperty("Count").GetInt32()).IsEqualTo(3);
            await Assert.That(changed.GetProperty("Total").GetDecimal()).IsEqualTo(3 * (product.GetProperty("Price").GetDecimal() + green.GetProperty("Adjustment").GetDecimal()));
            var removed = await second.PostJsonAsync("stationery/api/cart/quantity", new
            {
                lineId,
                quantity = 0
            });
            await Assert.That(removed.GetProperty("Count").GetInt32()).IsEqualTo(0);
            using var csrf = await outsider.PostAsync("stationery/api/cart/add", selection, includeToken: false);
            await Assert.That(csrf.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That((await outsider.CartAsync()).GetProperty("Count").GetInt32()).IsEqualTo(0);
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
        await Assert.That(document.GetProperty("paths").EnumerateObject().Count() >= 7).IsTrue();
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

    private static object Selection(JsonElement product, JsonElement attribute, JsonElement option)
    {
        return new
        {
            productId = product.GetProperty("Id").GetInt32(),
            values = new Dictionary<int, int>
            {
                [attribute.GetProperty("Id").GetInt32()] = option.GetProperty("Id").GetInt32()
            },
            quantity = 1
        };
    }

    private static async Task<(JsonElement Product, JsonElement Attribute, JsonElement Green, JsonElement Orange)> Fixture(StorefrontClient client)
    {
        var catalog = await client.GetJsonAsync("stationery/api/catalog");
        var product = catalog.EnumerateArray().First(item => item.GetProperty("Available").GetBoolean() && item.GetProperty("Attributes").GetArrayLength() > 0);
        var attribute = product.GetProperty("Attributes")[0];
        var green = attribute.GetProperty("Options").EnumerateArray().First(option => option.GetProperty("Name").GetString() == "سبز");
        var orange = attribute.GetProperty("Options").EnumerateArray().First(option => option.GetProperty("Name").GetString() == "نارنجی");
        return (product, attribute, green, orange);
    }
}