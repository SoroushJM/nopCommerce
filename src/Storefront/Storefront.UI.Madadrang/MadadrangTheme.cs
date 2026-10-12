using Storefront.UI.Shared;

namespace Storefront.UI.Madadrang;

public static class MadadrangTheme
{
    public static StorefrontThemeProfile Profile { get; } = new()
    {
        BrandName = "مداد رنگی",
        CatalogUrl = "/stationery/catalog",
        LoginUrl = "/login",
        OtpApiBasePath = "/stationery/api/",
        EnableOtp = true,
        CartDescription = "یک جای کوچک برای انتخاب‌های رنگی",
        EmptyCartDescription = "برای شروع، یک همراه رنگی انتخاب کن.",
        HomeIcon = "pencil",
        SearchPlaceholder = "دنبال چی می‌گردی؟ دفتر، مداد، یک ایده تازه..."
    };
}
