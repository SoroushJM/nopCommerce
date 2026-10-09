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
        var url = new Uri(Environment.GetEnvironmentVariable("STOREFRONT_TEST_URL") ?? "http://localhost:5090/");
        if (!url.IsLoopback || url.Scheme != "http" || url.UserInfo.Length > 0)
            throw new InvalidOperationException("Storefront integration tests require an HTTP loopback development host.");
        _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false })
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

    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.Clone();
    }

    public Task<JsonElement> CartAsync()
    {
        return GetJsonAsync("stationery/api/cart");
    }

    public async Task ClearCartAsync()
    {
        var cart = await CartAsync();
        foreach (var line in cart.GetProperty("Lines").EnumerateArray())
        {
            await PostJsonAsync("stationery/api/cart/quantity", new
            {
                lineId = line.GetProperty("Id").GetInt32(),
                quantity = 0
            });
        }
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }
}