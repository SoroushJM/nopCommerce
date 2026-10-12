using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nop.Storefront.Tests;

internal sealed class StorefrontClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    public string Token { get; private set; } = "";
    public static int ProductId => int.Parse(Environment.GetEnvironmentVariable("STOREFRONT_TEST_PRODUCT_ID") ?? "1");

    public StorefrontClient()
    {
        var url = StorefrontTestTarget.Resolve(Environment.GetEnvironmentVariable("STOREFRONT_TEST_URL"),
            Environment.GetEnvironmentVariable("STOREFRONT_TEST_ISOLATED"));
        _http = new HttpClient(new StorefrontTargetHandler(url))
        {
            BaseAddress = url,
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<string> InitializeAsync(string path = "cart")
    {
        var html = await _http.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success)
            throw new InvalidOperationException("Page did not provide an antiforgery token.");
        Token = WebUtility.HtmlDecode(match.Groups[1].Value);
        return html;
    }

    public Task<HttpResponseMessage> GetAsync(string path)
    {
        return _http.GetAsync(path);
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await GetAsync(path);
        return await ReadJsonAsync(response);
    }

    public async Task<HttpResponseMessage> PostAsync(string path, object data, bool includeToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(data) };
        if (includeToken)
            request.Headers.Add("RequestVerificationToken", Token);
        return await _http.SendAsync(request);
    }

    public async Task<JsonElement> PostJsonAsync(string path, object data)
    {
        using var response = await PostAsync(path, data);
        return await ReadJsonAsync(response);
    }

    public Task<HttpResponseMessage> PostFormAsync(string path, Dictionary<string, string> fields)
    {
        return _http.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    public async Task<JsonElement> NativeCartAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "cart");
        request.Headers.Add("X-Madadrang-Cart", "1");
        using var response = await _http.SendAsync(request);
        var cart = await ReadJsonAsync(response);
        Token = cart.GetProperty("Token").GetString()!;
        return cart;
    }

    public async Task<HttpResponseMessage> PostNativeCartAsync(IEnumerable<KeyValuePair<string, string>> fields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "cart") { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("X-Madadrang-Cart", "1");
        return await _http.SendAsync(request);
    }

    public async Task<JsonElement> UploadCheckoutFileAsync(int attributeId)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent("TUnit checkout attachment"u8.ToArray()), "file", "tunit-checkout.txt");
        using var response = await _http.PostAsync($"shoppingcart/uploadfilecheckoutattribute?attributeId={attributeId}", body);
        return await ReadJsonAsync(response);
    }

    public async Task<JsonElement> UploadProductPictureAsync(int productId, string imageName)
    {
        var bytes = await _http.GetByteArrayAsync("_content/Storefront.UI/images/" + imageName);
        using var body = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        body.Add(file, "file", imageName);
        using var response = await _http.PostAsync($"Admin/Product/ProductPictureAdd?productId={productId}", body);
        return await ReadJsonAsync(response);
    }

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.Clone();
    }

    public async Task<JsonElement> UpdateNativeQuantityAsync(int lineId, int quantity)
    {
        var cart = await NativeCartAsync();
        var fields = new List<KeyValuePair<string, string>>
        {
            new("updatecart", "1"),
            new(cart.GetProperty("TokenField").GetString()!, Token)
        };
        foreach (var field in cart.GetProperty("CheckoutFields").EnumerateArray())
            foreach (var value in field.GetProperty("Values").EnumerateArray())
                fields.Add(new(field.GetProperty("Name").GetString()!, value.GetString()!));
        fields.Add(quantity == 0 ? new("removefromcart", lineId.ToString()) : new($"itemquantity{lineId}", quantity.ToString()));
        using var response = await PostNativeCartAsync(fields);
        return await ReadJsonAsync(response);
    }

    public async Task ClearCartAsync()
    {
        // Read the full page: mini-cart Lines can be capped to zero or disabled.
        var html = await InitializeAsync();
        var ids = Regex.Matches(html, "data-cart-itemid=\"([0-9]+)\"")
            .Select(match => match.Groups[1].Value).Distinct().ToArray();
        if (ids.Length == 0)
            return;
        var fields = new List<KeyValuePair<string, string>>
        {
            new("updatecart", "1"),
            new("__RequestVerificationToken", Token)
        };
        fields.AddRange(ids.Select(id => new KeyValuePair<string, string>("removefromcart", id)));
        using var response = await PostNativeCartAsync(fields);
        var cart = await ReadJsonAsync(response);
        if (cart.GetProperty("Count").GetInt32() != 0)
            throw new InvalidOperationException("Native cart cleanup left items behind.");
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }
}
