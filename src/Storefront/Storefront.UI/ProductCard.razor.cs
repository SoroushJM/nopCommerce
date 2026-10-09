using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Storefront.UI;

public partial class ProductCard
{
    [Parameter, EditorRequired]
    public StoreProduct Product { get; set; } = default!;

    private static string Money(decimal value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}