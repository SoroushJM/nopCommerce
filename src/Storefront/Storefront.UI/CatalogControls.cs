namespace Storefront.UI;

public sealed record CatalogChoice(string Value, string Text, bool Selected = false, string Color = "");
public sealed record CatalogFilterGroup(int Id, string Name, List<CatalogChoice> Options);
public sealed record CatalogFilterChange(string Key, string? Value);

public sealed class CatalogControls
{
    public string Path { get; set; } = "";
    public Dictionary<string, string> Query { get; set; } = [];
    public List<CatalogChoice> SortOptions { get; set; } = [];
    public List<CatalogChoice> PageSizes { get; set; } = [];
    public List<CatalogChoice> ViewModes { get; set; } = [];
    public List<CatalogChoice> Categories { get; set; } = [];
    public List<CatalogChoice> Manufacturers { get; set; } = [];
    public List<CatalogFilterGroup> Specifications { get; set; } = [];
    public bool NativeSearch
    {
        get; set;
    }
    public bool PriceEnabled
    {
        get; set;
    }
    public string PriceFrom { get; set; } = "";
    public string PriceTo { get; set; } = "";
    public string AvailableFrom { get; set; } = "";
    public string AvailableTo { get; set; } = "";
    public string Currency { get; set; } = "";
    public string DecimalSeparator { get; set; } = ".";
}