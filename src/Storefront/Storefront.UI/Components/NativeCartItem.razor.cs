using BlazorBlueprint.Primitives;
using Microsoft.AspNetCore.Components;
using BlazorBlueprint.Primitives.Services;

namespace Storefront.UI.Components;

public partial class NativeCartItem
{
    [Inject]
    private IFocusManager FocusManager { get; set; } = default!;
    [Parameter, EditorRequired]
    public NativeCartLine Line { get; set; } = default!;
    [Parameter]
    public bool ShowImage
    {
        get; set;
    }
    [Parameter]
    public bool Busy
    {
        get; set;
    }
    [Parameter]
    public EventCallback<QuantityRequest> OnQuantity
    {
        get; set;
    }

    private List<SelectOption<string>> Options => Line.AllowedQuantities.Select(value => new SelectOption<string>(value.Value, value.Text)).ToList();
    private int _selectionRevision;
    private bool _restoreFocus;
    private ElementReference _quantityControls;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_restoreFocus)
            return;
        _restoreFocus = false;
        await FocusManager.FocusFirst(_quantityControls);
    }

    private Task SetQuantity(int quantity)
    {
        return OnQuantity.InvokeAsync(new QuantityRequest(Line.Id, quantity));
    }

    private async Task SelectQuantity(string? value)
    {
        try
        {
            if (int.TryParse(value, out var quantity))
                await SetQuantity(quantity);
        }
        finally
        {
            // BB keeps its chosen label when native validation retains the saved quantity.
            _selectionRevision++;
            _restoreFocus = true;
        }
    }
}