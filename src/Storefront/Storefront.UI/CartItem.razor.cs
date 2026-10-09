using Microsoft.AspNetCore.Components;

namespace Storefront.UI;

public partial class CartItem
{
    [Parameter, EditorRequired]
    public CartLine Line { get; set; } = default!;

    [Parameter]
    public EventCallback<QuantityRequest> OnQuantity
    {
        get; set;
    }

    private Task SetQuantity(int quantity)
    {
        return OnQuantity.InvokeAsync(new QuantityRequest(Line.Id, quantity));
    }
}