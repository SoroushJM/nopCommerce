using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;
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
    private string _fullSizeImage = "", _imageAlt = "", _imageTitle = "", _stock = "", _sku = "", _mpn = "", _gtin = "", _basePrice = "";
    private List<int> _pictureIds = [];
    private NativeAttributeResult? _pendingAttributes;
    private int _quantity, _selectionVersion;
    private int _quantityInputRevision;
    private ElementReference _quantityInput;
    private ElementReference _gallery;
    private bool _restoreZoomFocus;
    private bool _restoreQuantityFocus;
    private bool _ready, _busy, _updating, _zoomOpen, _freeShipping, _quantityChanged;
    private string PurchaseLabel => StockAvailable ? Initial.PurchaseLabel : "فعلاً ناموجود";
    private string StockLabel => _stock.Length > 0 ? _stock : StockAvailable ? "" : "این انتخاب فعلاً ناموجود است";
    private IEnumerable<NativeProductPicture> VisiblePictures => Initial.Pictures.Where(picture =>
        !Initial.CombinationImagesOnly || _pictureIds.Count == 0 || _pictureIds.Contains(picture.Id));
    private bool DisplayTierPrices => !Initial.HidePrices && !Initial.CallForPrice && Initial.TierPrices.Count > 0
        && !(Initial.TierPrices.Count == 1 && Initial.TierPrices[0].Quantity <= 1);

    private bool Available => !Initial.DisableBuy && StockAvailable;

    private bool StockAvailable
    {
        get
        {
            if (!Initial.StockByAttributes)
                return Initial.InStock;
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
        _fullSizeImage = Initial.FullSizeImage;
        _imageAlt = Initial.ImageAlt;
        _imageTitle = Initial.ImageTitle;
        _stock = Initial.Stock ?? "";
        _sku = Initial.Sku;
        _mpn = Initial.Mpn;
        _gtin = Initial.Gtin;
        _basePrice = Initial.BasePrice ?? "";
        _freeShipping = Initial.IsFreeShipping;
        _quantity = Initial.Quantity;
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
        {
            if (_restoreZoomFocus && _module != null)
            {
                _restoreZoomFocus = false;
                await _module.InvokeVoidAsync("focusProductZoomTrigger", _gallery);
            }
            if (_restoreQuantityFocus)
            {
                _restoreQuantityFocus = false;
                await _quantityInput.FocusAsync();
            }
            if (_module != null && (_pendingAttributes != null || _quantityChanged))
            {
                var attributes = _pendingAttributes;
                var quantityChanged = _quantityChanged;
                _pendingAttributes = null;
                _quantityChanged = false;
                await _module.InvokeVoidAsync("notifyProductChanged", Initial.Id, attributes, quantityChanged ? _quantity : null);
            }
            return;
        }
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
            if (!Initial.HidePrices && !Initial.CallForPrice)
                _price = result.Price ?? "";
            _stock = result.StockAvailability ?? "";
            _sku = result.Sku ?? "";
            _mpn = result.Mpn ?? "";
            _gtin = result.Gtin ?? "";
            _basePrice = result.Basepricepangv ?? "";
            _freeShipping = result.IsFreeShipping;
            _pictureIds = result.PictureIds;
            if (!string.IsNullOrWhiteSpace(result.PictureDefaultSizeUrl))
            {
                _image = result.PictureDefaultSizeUrl;
                _fullSizeImage = string.IsNullOrWhiteSpace(result.PictureFullSizeUrl) ? _image : result.PictureFullSizeUrl;
                var picture = Initial.Pictures.FirstOrDefault(item => item.Image == _image);
                _imageAlt = picture?.Alt ?? Initial.ImageAlt;
                _imageTitle = picture?.Title ?? Initial.ImageTitle;
            }
            _disabledAttributes.Clear();
            _disabledAttributes.UnionWith(result.DisabledAttributeMappingIds);
            _error = string.Join(" ", result.Message ?? []);
            _pendingAttributes = result;
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
        _quantityChanged = true;
        try
        {
            await RefreshSelection();
        }
        catch (Exception)
        {
            _error = "به‌روزرسانی تعداد انجام نشد. دوباره تلاش کنید.";
        }
    }

    private async Task EnterQuantity(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
            await ChangeQuantity(quantity);
        else
        {
            _quantityInputRevision++;
            _restoreQuantityFocus = true;
            _error = "تعداد باید عدد صحیح باشد؛ مقدار قبلی دوباره نمایش داده شد.";
        }
    }

    private void SelectPicture(NativeProductPicture picture)
    {
        _image = picture.Image;
        _fullSizeImage = picture.FullSize;
        _imageAlt = picture.Alt;
        _imageTitle = picture.Title;
    }

    private void SetZoomOpen(bool open)
    {
        _zoomOpen = open;
        _restoreZoomFocus = !open;
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