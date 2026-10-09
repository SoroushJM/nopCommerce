namespace Storefront.UI;

public sealed record NativeFormField(string Name, List<string> Values);
public sealed record NativeCartWarning(int ItemId, string ProductName, List<string> Messages);
public sealed record NativeCartTotalRow(string Label, string Value, string Detail = "", bool Emphasis = false);

public sealed record NativeCart
{
    public bool MiniEnabled
    {
        get; init;
    }
    public bool SignedIn
    {
        get; init;
    }
    public string Phone { get; init; } = "";
    public int Count
    {
        get; init;
    }
    public string CartUrl { get; init; } = "";
    public string CheckoutUrl { get; init; } = "";
    public bool DisplayShoppingCartButton
    {
        get; init;
    }
    public bool DisplayCheckoutButton
    {
        get; init;
    }
    public bool ShowProductImages
    {
        get; init;
    }
    public string MiniSubTotal { get; init; } = "";
    public List<NativeCartLine> Lines { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<NativeCartWarning> ItemWarnings { get; init; } = [];
    public List<NativeCartTotalRow> Totals { get; init; } = [];
    public List<NativeFormField> CheckoutFields { get; init; } = [];
    public string TokenField { get; init; } = "";
    public string Token { get; init; } = "";
}

public sealed record NativeCartLine
{
    public int Id
    {
        get; init;
    }
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string EditUrl { get; init; } = "";
    public string Image { get; init; } = "";
    public string ImageAlt { get; init; } = "";
    public string ImageTitle { get; init; } = "";
    public int Quantity
    {
        get; init;
    }
    public string UnitPrice { get; init; } = "";
    public string SubTotal { get; init; } = "";
    public string Discount { get; init; } = "";
    public string DiscountNote { get; init; } = "";
    public string AttributeInfo { get; init; } = "";
    public string RecurringInfo { get; init; } = "";
    public string RentalInfo { get; init; } = "";
    public bool DisableRemoval
    {
        get; init;
    }
    public List<CatalogChoice> AllowedQuantities { get; init; } = [];
}