using System.Globalization;
using Microsoft.AspNetCore.Components;
using BlazorBlueprint.Primitives;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;
using Storefront.UI.Shared;

namespace Storefront.UI.Shared.Components;

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
    private bool _ready, _selectionReady, _busy, _updating, _zoomOpen, _freeShipping, _quantityChanged;
    private NativeUiLabels Labels => Initial.Labels;
    private string PurchaseLabel => StockAvailable ? Initial.PurchaseLabel : Labels.Get("Products.Availability.OutOfStock");
    private string StockLabel => _stock.Length > 0 ? _stock : StockAvailable ? "" : Labels.Get("Products.Availability.OutOfStock");
    private IEnumerable<NativeProductPicture> VisiblePictures => Initial.Pictures.Where(picture =>
        !Initial.CombinationImagesOnly || _pictureIds.Count == 0 || _pictureIds.Contains(picture.Id));
    private bool DisplayTierPrices => !Initial.HidePrices && !Initial.CallForPrice && Initial.TierPrices.Count > 0
        && !(Initial.TierPrices.Count == 1 && Initial.TierPrices[0].Quantity <= 1);

    private bool Available => !Initial.DisableBuy && StockAvailable;

    private bool StockAvailable
    {
        get
        {
            if (!Initial.StockByAttributes || !_selectionReady)
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
        _disabledAttributes.UnionWith(Initial.InitialDisabledAttributeIds);
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
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Storefront.UI.Shared/storefront.js");
            _combinations = await _module.InvokeAsync<List<NativeCombination>>("request", Initial.CombinationsUrl);
            _ready = true;
            await RefreshSelection();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            _error = Labels.Get("Common.Error");
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
        if (!_ready || _busy)
            return;
        _selected[attributeId] = valueId;
        await RefreshSelection();
    }

    private async Task RefreshSelection()
    {
        if (_module == null)
            return;
        var version = ++_selectionVersion;
        _selectionReady = false;
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
            _selectionReady = true;
        }
        catch (Exception)
        {
            if (version == _selectionVersion)
                _error = Labels.Get("Common.Error");
        }
        finally
        {
            if (version == _selectionVersion)
                _updating = false;
        }
    }

    private async Task ChangeQuantity(int quantity)
    {
        if (!_ready || _busy)
            return;
        _quantity = quantity;
        _quantityChanged = true;
        await RefreshSelection();
    }

    private async Task EnterQuantity(ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
            await ChangeQuantity(quantity);
        else
        {
            _quantityInputRevision++;
            _restoreQuantityFocus = true;
            _error = Labels.Get("Products.QuantityShouldBePositive");
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
        if (_module == null || _busy || _updating || !_ready || !_selectionReady || !Available)
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
            _error = Labels.Get("Common.Error");
        }
        finally
        {
            _busy = false;
        }
    }

    private static string AttributePrompt(NativeAttribute attribute) =>
        string.IsNullOrEmpty(attribute.TextPrompt) ? attribute.Name : attribute.TextPrompt;

    private string OptionLabel(NativeOption option) =>
        Initial.HidePrices || Initial.CallForPrice || string.IsNullOrEmpty(option.DisplayName)
            ? option.Name : option.DisplayName;

    private IEnumerable<SelectOption<int>> AttributeOptions(NativeAttribute attribute)
    {
        yield return new SelectOption<int>(0, attribute.Required ? Labels.Get("Products.ProductAttributes.DropdownList.DefaultItem") : "---");
        foreach (var option in attribute.Options)
            yield return new SelectOption<int>(option.Id, OptionLabel(option));
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
            await _module.DisposeAsync();
    }
}
