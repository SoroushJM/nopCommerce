using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Storefront.UI;

namespace Storefront.UI.Components;

public partial class NativeProductPage
{
    [Parameter]
    public NativeProduct Initial { get; set; } = new();

    private IJSObjectReference? _module;
    private readonly Dictionary<int, int> _selected = [];
    private readonly HashSet<int> _disabledAttributes = [];
    private List<NativeCombination> _combinations = [];
    private string _price = "", _image = "", _error = "";
    private int _quantity, _selectionVersion;
    private bool _ready, _busy, _updating;
    private string PurchaseLabel => Available ? "افزودن به سبد خرید" : "فعلاً ناموجود";
    private string StockLabel => Available ? "موجود و آمادهٔ انتخاب" : "این انتخاب فعلاً ناموجود است";

    private bool Available
    {
        get
        {
            if (Initial.DisableBuy || !Initial.InStock)
                return false;
            if (!Initial.StockByAttributes)
                return true;
            var active = _selected.Where(item => item.Value > 0 && !_disabledAttributes.Contains(item.Key)).ToList();
            var match = _combinations.FirstOrDefault(combination => combination.Attributes.Count == active.Count && combination.Attributes.All(attribute => active.Any(item => item.Key == attribute.Id && attribute.ValueIds.Contains(item.Value))));
            if (match != null)
                return match.InStock;
            return !Initial.ExistingCombinationsOnly;
        }
    }

    protected override void OnInitialized()
    {
        _price = Initial.Price;
        _image = Initial.Image;
        _quantity = Math.Max(1, Initial.Quantity);
        foreach (var attribute in Initial.Attributes)
        {
            var option = attribute.Options.FirstOrDefault(item => item.Selected);
            if (option != null)
                _selected[attribute.Id] = option.Id;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Storefront.UI/storefront.js");
            _combinations = await _module.InvokeAsync<List<NativeCombination>>("request", Initial.CombinationsUrl);
            await RefreshSelection();
            _ready = true;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            _error = "بارگذاری کنترل‌های محصول انجام نشد. صفحه را دوباره باز کنید.";
        }

        StateHasChanged();
    }

    private Dictionary<string, string> FormFields()
    {
        var fields = _selected.ToDictionary(item => "product_attribute_" + item.Key, item => item.Value.ToString(CultureInfo.InvariantCulture));
        fields["addtocart_" + Initial.Id + ".EnteredQuantity"] = _quantity.ToString(CultureInfo.InvariantCulture);
        if (Initial.CartItemId > 0)
            fields["addtocart_" + Initial.Id + ".UpdatedShoppingCartItemId"] = Initial.CartItemId.ToString(CultureInfo.InvariantCulture);
        return fields;
    }

    private async Task SelectOption(int attributeId, int valueId)
    {
        _selected[attributeId] = valueId;
        try
        {
            await RefreshSelection();
        }
        catch (Exception)
        {
            _error = "به‌روزرسانی انتخاب انجام نشد. دوباره تلاش کنید.";
        }
    }

    private async Task RefreshSelection()
    {
        if (_module == null)
            return;
        var version = ++_selectionVersion;
        _updating = true;
        try
        {
            var result = await _module.InvokeAsync<NativeAttributeResult>("postForm", Initial.ChangeUrl, FormFields());
            if (version != _selectionVersion)
                return;
            if (!string.IsNullOrWhiteSpace(result.Price))
                _price = result.Price;
            if (!string.IsNullOrWhiteSpace(result.PictureDefaultSizeUrl))
                _image = result.PictureDefaultSizeUrl;
            _disabledAttributes.Clear();
            _disabledAttributes.UnionWith(result.DisabledAttributeMappingIds);
            _error = "";
        }
        finally
        {
            if (version == _selectionVersion)
                _updating = false;
        }
    }

    private async Task ChangeQuantity(int quantity)
    {
        _quantity = quantity;
        try
        {
            await RefreshSelection();
        }
        catch (Exception)
        {
            _error = "به‌روزرسانی تعداد انجام نشد. دوباره تلاش کنید.";
        }
    }

    private async Task AddToCart()
    {
        if (_module == null || _busy || _updating || !_ready || !Available)
            return;
        _busy = true;
        try
        {
            var result = await _module.InvokeAsync<NativeCartResult>("postForm", Initial.AddUrl, FormFields());
            if (result.Redirect != null)
            {
                Navigation.NavigateTo(result.Redirect, forceLoad: true);
                return;
            }

            if (!result.Success)
            {
                _error = string.Join(" ", result.Message);
                return;
            }

            await _module.InvokeVoidAsync("notifyCartChanged", true);
            _error = "";
        }
        catch (Exception)
        {
            _error = "افزودن به سبد انجام نشد. دوباره تلاش کنید.";
        }
        finally
        {
            _busy = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
            await _module.DisposeAsync();
    }
}