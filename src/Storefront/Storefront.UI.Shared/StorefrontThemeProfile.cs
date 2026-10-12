namespace Storefront.UI.Shared;

/// <summary>Presentation and navigation supplied by the storefront's selected theme.</summary>
public sealed record StorefrontThemeProfile
{
    public string BrandName { get; init; } = "فروشگاه";
    public string CatalogUrl { get; init; } = "/catalog/all";
    public string LoginUrl { get; init; } = "/login";
    public string OtpApiBasePath { get; init; } = "/storefront/api";
    public bool EnableOtp { get; init; }
    public string CartDescription { get; init; } = "محصولات انتخاب‌شدهٔ تو";
    public string EmptyCartDescription { get; init; } = "محصولی را برای شروع انتخاب کن.";
    public string HomeIcon { get; init; } = "home";
    public string SearchPlaceholder { get; init; } = "جست‌وجوی محصولات";
}
