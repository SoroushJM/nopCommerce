using System.Text.Json.Serialization;

namespace Storefront.UI;

public sealed record StoreLink(int Id, string Name, string Url);
public sealed record NativeOption(int Id, string Name, string Color, string Image, bool Selected);
public sealed record NativeAttribute(int Id, string Name, int ControlType, bool Required, bool HasCondition, List<NativeOption> Options);
public sealed record NativeProductPicture(int Id, string Image, string Thumbnail, string FullSize, string Alt, string Title);
public sealed record NativeTierPrice(int Quantity, string Price);

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
    public string FullSizeImage { get; init; } = "";
    public string ImageTitle { get; init; } = "";
    public bool ZoomEnabled
    {
        get; init;
    }
    public bool CombinationImagesOnly
    {
        get; init;
    }
    public List<NativeProductPicture> Pictures { get; init; } = [];
    public string Price { get; init; } = "";
    public string ReferencePrice { get; init; } = "";
    public string OldPrice { get; init; } = "";
    public string BasePrice { get; init; } = "";
    public bool HidePrices
    {
        get; init;
    }
    public bool CallForPrice
    {
        get; init;
    }
    public string CallForPriceLabel { get; init; } = "";
    public string TaxShippingInfo { get; init; } = "";
    public List<NativeTierPrice> TierPrices { get; init; } = [];
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
    public int MinimumQuantity { get; init; } = 1;
    public string MinimumQuantityNotification { get; init; } = "";
    public string PurchaseLabel { get; init; } = "";
    public string PreOrderDate { get; init; } = "";
    public string Sku { get; init; } = "";
    public bool ShowSku
    {
        get; init;
    }
    public string Mpn { get; init; } = "";
    public bool ShowMpn
    {
        get; init;
    }
    public string Gtin { get; init; } = "";
    public bool ShowGtin
    {
        get; init;
    }
    public bool ShowVendor
    {
        get; init;
    }
    public StoreLink? Vendor
    {
        get; init;
    }
    public List<StoreLink> Manufacturers { get; init; } = [];
    public bool IsShipEnabled
    {
        get; init;
    }
    public bool IsFreeShipping
    {
        get; init;
    }
    public bool FreeShippingNotificationEnabled
    {
        get; init;
    }
    public string DeliveryDate { get; init; } = "";
    public bool BreadcrumbEnabled
    {
        get; init;
    }
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
    public string Sku { get; set; } = "";
    public string Mpn { get; set; } = "";
    public string Gtin { get; set; } = "";
    public string Basepricepangv { get; set; } = "";
    public bool IsFreeShipping
    {
        get; set;
    }
    public string[]? Message
    {
        get; set;
    }
    public string PictureDefaultSizeUrl { get; set; } = "";
    public string PictureFullSizeUrl { get; set; } = "";
    public List<int> PictureIds { get; set; } = [];
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