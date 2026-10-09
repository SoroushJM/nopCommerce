using System.Text.Json.Serialization;

namespace Storefront.UI;

public sealed record StoreLink(int Id, string Name, string Url);
public sealed record NativeOption(int Id, string Name, string Color, string Image, bool Selected);
public sealed record NativeAttribute(int Id, string Name, int ControlType, bool Required, bool HasCondition, List<NativeOption> Options);

public sealed record NativeProduct
{
    public int Id
    {
        get; init;
    }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Image { get; init; } = "";
    public string ImageAlt { get; init; } = "";
    public string Price { get; init; } = "";
    public string OldPrice { get; init; } = "";
    public string Stock { get; init; } = "";
    public bool InStock
    {
        get; init;
    }
    public bool DisableBuy
    {
        get; init;
    }
    public bool ExistingCombinationsOnly
    {
        get; init;
    }
    public bool StockByAttributes
    {
        get; init;
    }
    public int Quantity { get; init; } = 1;
    public int CartItemId
    {
        get; init;
    }
    public List<int> Quantities { get; init; } = [];
    public List<NativeAttribute> Attributes { get; init; } = [];
    public List<StoreLink> Breadcrumb { get; init; } = [];
    public string Brand { get; init; } = "";
    public string ChangeUrl { get; init; } = "";
    public string AddUrl { get; init; } = "";
    public string CombinationsUrl { get; init; } = "";
}

public sealed record NativeAttributeResult
{
    public string Price { get; set; } = "";
    public string StockAvailability { get; set; } = "";
    public string PictureDefaultSizeUrl { get; set; } = "";
    [JsonPropertyName("enabledattributemappingids")]
    public List<int> EnabledAttributeMappingIds { get; set; } = [];
    [JsonPropertyName("disabledattributemappingids")]
    public List<int> DisabledAttributeMappingIds { get; set; } = [];
}

public sealed record NativeCartResult
{
    public bool Success
    {
        get; set;
    }
    public string? Redirect
    {
        get; set;
    }
    public string[] Message { get; set; } = [];
}

public sealed record NativeCombination
{
    public List<CombinationAttribute> Attributes { get; set; } = [];
    public bool InStock
    {
        get; set;
    }
}

public sealed record CombinationAttribute
{
    public int Id
    {
        get; set;
    }
    public List<int> ValueIds { get; set; } = [];
}