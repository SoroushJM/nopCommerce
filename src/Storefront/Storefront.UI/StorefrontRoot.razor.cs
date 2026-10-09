using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;

namespace Storefront.UI;

public partial class StorefrontRoot
{
    [Parameter]
    public Bootstrap Initial { get; set; } = new([], new([]), false);

    [Parameter]
    public string Page { get; set; } = "home";

    [Parameter]
    public int ProductId
    {
        get; set;
    }

    private List<StoreProduct> products = [];
    private CartSnapshot cart = new([]);
    private IJSObjectReference? module;
    private string query = "", category = "", brand = "", colorFilter = "", sort = "popular", error = "", phone = "", code = "", testCode = "", loginMessage = "";
    private decimal maxPrice = 1000000;
    private static readonly SelectOption<string>[] SortOptions = [new("popular", "محبوب‌ترین"), new("price", "ارزان‌ترین"), new("price-desc", "گران‌ترین")];
    private static readonly SelectOption<decimal>[] PriceOptions = [new(1000000m, "همهٔ قیمت‌ها"), new(200000m, "تا 200,000 تومان"), new(400000m, "تا 400,000 تومان")];
    private IEnumerable<SelectOption<string>> BrandOptions => new[]
    {
        new SelectOption<string>("", "همهٔ برندها")
    }.Concat(products.Select(p => p.Brand).Distinct().Select(b => new SelectOption<string>(b, b)));

    private bool onlyAvailable, cartOpen, loginOpen, otpSent, busy, checkoutReady;
    private int quantity = 1;
    private Dictionary<int, int> selected = [];
    private Quote quote = new(0, false);
    private static readonly string[] Categories = ["دفتر و کاغذ", "نوشت‌افزار", "لوازم مدرسه", "هنر و خلاقیت"];
    private static bool IsColorAttribute(ProductAttribute attribute) => attribute.Name.Contains("رنگ") || attribute.Name.Contains("color", StringComparison.OrdinalIgnoreCase);
    private IEnumerable<string> ColorNames => products.SelectMany(p => p.Attributes).Where(IsColorAttribute).SelectMany(a => a.Options).Select(o => o.Name).Distinct();
    private StoreProduct? CurrentProduct => products.FirstOrDefault(x => x.Id == ProductId);

    private IEnumerable<StoreProduct> Filtered
    {
        get
        {
            var list = products.Where(x => (category == "" || x.Category == category) && (brand == "" || x.Brand == brand) && (colorFilter == "" || x.Attributes.Where(IsColorAttribute).Any(a => a.Options.Any(o => o.Name == colorFilter))) && x.Price <= maxPrice && (!onlyAvailable || x.Available) && (query == "" || $"{x.Name} {x.Brand} {x.Category}".Contains(query, StringComparison.OrdinalIgnoreCase)));
            return sort == "price" ? list.OrderBy(x => x.Price) : sort == "price-desc" ? list.OrderByDescending(x => x.Price) : list;
        }
    }

    protected override void OnInitialized()
    {
        products = Initial.Products;
        cart = Initial.Cart;
        if (Page == "login")
        {
            Page = "home";
            loginOpen = true;
        }

        var qs = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(Navigation.Uri).Query);
        query = qs.GetValueOrDefault("q").ToString();
        category = qs.GetValueOrDefault("category").ToString();
        sort = qs.TryGetValue("sort", out var requestedSort) && requestedSort.ToString() != "" ? requestedSort.ToString() : "popular";
        if (CurrentProduct is { } p)
        {
            foreach (var a in p.Attributes)
            {
                if (a.Options.FirstOrDefault() is { } o)
                    selected[a.Id] = o.Id;
            }

            quote = new(p.Price, p.Available);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Storefront.UI/storefront.js");
            await Run(async () =>
            {
                cart = await Api<CartSnapshot>("cart");
                if (CurrentProduct != null)
                    await RefreshQuote();
            });
            StateHasChanged();
        }
    }

    private async Task<T> Api<T>(string path, string method = "GET", object? data = null) => await module!.InvokeAsync<T>("request", Initial.ApiBase + "/" + path, method, data);
    private async Task Run(Func<Task> action)
    {
        if (module == null)
            return;
        busy = true;
        try
        {
            await action();
            error = "";
        }
        catch (Exception)
        {
            error = "انجام این کار ممکن نشد. اتصال و انتخاب محصول را بررسی کن و دوباره تلاش کن.";
        }
        finally
        {
            busy = false;
        }
    }

    private async Task SelectOption(int attribute, int value)
    {
        selected[attribute] = value;
        await Run(RefreshQuote);
    }

    private async Task ChangeProductQuantity(int delta)
    {
        quantity = Math.Clamp(quantity + delta, 1, 20);
        await Run(RefreshQuote);
    }

    private async Task RefreshQuote() => quote = await Api<Quote>("quote", "POST", new SelectionRequest(ProductId, selected, quantity));
    private async Task AddToCart() => await Run(async () =>
    {
        cart = await Api<CartSnapshot>("cart/add", "POST", new SelectionRequest(ProductId, selected, quantity));
        cartOpen = true;
    });
    private async Task UpdateQuantity(QuantityRequest request) => await Run(async () => cart = await Api<CartSnapshot>("cart/quantity", "POST", request));
    private void Search() => Go("/stationery/catalog?q=" + Uri.EscapeDataString(query));
    private void Go(string url) => Navigation.NavigateTo(url, true);
    private void GoHome() => Go("/");
    private void GoCatalog() => Go("/stationery/catalog");
    private void ResetFilters()
    {
        query = "";
        category = "";
        brand = "";
        colorFilter = "";
        maxPrice = 1000000;
        onlyAvailable = false;
        sort = "popular";
    }

    private void ShowLogin()
    {
        loginOpen = true;
        loginMessage = "";
    }

    private void Checkout()
    {
        if (cart.SignedIn)
            checkoutReady = true;
        else
            ShowLogin();
    }

    private async Task SubmitLogin()
    {
        await Run(async () =>
        {
            var result = otpSent ? await Api<OtpResult>("otp/verify", "POST", new VerifyRequest(phone, code)) : await Api<OtpResult>("otp/send", "POST", new PhoneRequest(phone));
            loginMessage = result.Message;
            if (result.TestCode != null)
                testCode = result.TestCode;
            if (result.Success)
            {
                if (otpSent)
                {
                    cart = await Api<CartSnapshot>("cart");
                    loginOpen = false;
                    otpSent = false;
                    code = "";
                    Go("/stationery/cart");
                }
                else
                    otpSent = true;
            }
        });
    }

    private static string Money(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);
    private static string CatalogUrl(string category) => "/stationery/catalog?category=" + Uri.EscapeDataString(category);
    private static string CategoryBackground(int i) => new[]
    {
        "#edf3ff",
        "#fff1e5",
        "#f1edf9",
        "#ecf5ed"
    }[i];
    private static string CategoryColor(int i) => new[]
    {
        "#5a7ccc",
        "#cd8748",
        "#9680bc",
        "#71a27e"
    }[i];
    public async ValueTask DisposeAsync()
    {
        if (module != null)
            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
    }
}