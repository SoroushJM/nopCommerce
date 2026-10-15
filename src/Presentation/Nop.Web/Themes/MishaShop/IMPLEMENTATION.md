# MishaShop integration and feature coverage

The approved clothing-store UI uses cream, nude and ivory colors, a centered configured store brand, a split hero with a real configured photo, round category links, two product columns on mobile, portrait product photos, a main image with thumbnails, minimal size controls and round color swatches, an inline add-to-cart action, accordion details, a compact cart, rounded checkout cards and a minimal login page. The orders page retains the native customer interface because its visual choice was not finalized.

The theme never supplies fixture products, fake prices or stock clothing photos. `MishaShopHero` accepts configured brand, heading, description, photo URL/alt text and catalog URL. Without a configured photo it presents a neutral photo slot. Setup guidance appears only when `ShowSetupHint` is explicitly enabled by the server for administrators.

`Storefront.UI.MishaShop` is an independent net10 Razor library referencing `Storefront.UI.Shared`, Blazor Blueprint 4.1.0 and Tailwind 4.1.18. Its pinned lockfile builds `wwwroot/mishashop.css`. Font files include their OFL license. The theme stylesheet scans its own MVC views and the shared Razor components.

## Host contracts

- Root view: `_MishaShopRoot` (also reached through themed `_Root`, `_ColumnsOne`, `_ColumnsTwo`). Its complete native `_Root.Head` preserves scripts, metadata, antiforgery, custom head/footer, widget zones and cookie consent. The theme includes a path-base-aware `base` element for Razor component rendering.
- View components `MishaShopHeader` and `MishaShopFooter` return `~/Themes/MishaShop/Views/Shared/Components/MishaShopHeader/Default.cshtml` and the matching footer path. Model is `Storefront.UI.Shared.StoreNavigation`; optional `ViewData["ThemeProfile"]` is the shared `StorefrontThemeProfile`. Header also accepts a configured `ViewData["LogoUrl"]`.
- View component `MishaShopHero` returns `~/Themes/MishaShop/Views/Shared/Components/MishaShopHero/Default.cshtml` with `ViewData` keys `BrandName`, `Heading`, `Description`, `ImageUrl`, `ImageAlt`, `CatalogUrl`, and optional administrator-only `ShowSetupHint`.
- All-products controller returns `~/Themes/MishaShop/Views/Catalog/AllProducts.cshtml` with native `SearchModel`. Its search form submits to the full native `SEARCH` route so all advanced criteria remain functional. Disable native AJAX for an empty search term unless the server supplies an all-products-compatible AJAX route; its ordinary native search view uses `SEARCH_PRODUCTS`.
- Native product routes resolve `ProductTemplate.Simple.cshtml` / `ProductTemplate.Grouped.cshtml`. These preserve the complete native model, forms, all partials and widget zones. Shared `NativeProductPage` bridges can still be used when the host determines every enabled attribute/plugin feature is supported; these full native templates serve the complete fallback.

## Preserved storefront features

Native customer/cart/checkout forms are intentionally retained and styled through common layouts rather than replaced with a reduced model. This includes registration, login, MFA/password recovery, customer fields and consent, addresses, order list/detail/invoice/downloads, returns, recurring payments, reward points, wishlist and sharing, comparison, product reviews/helpfulness, back-in-stock notifications, contact topics, newsletter/social links, blog/news/forums/private messages and configured menu destinations. Availability depends on native store settings, permissions and installed plugins.

Catalog retains native category SEO/JSON-LD, breadcrumbs, subcategories, featured products, descriptions, search advanced fields, manufacturer/vendor/filter-level/specification/price filters, selectors, grid/list modes, pagination and AJAX. Product cards retain permitted buy/rental/preorder/wishlist/compare actions, SKU, ratings, prices, tax/shipping/base-price information and product-box widget zones.

Product templates retain all conditional attributes, uploaded files, customer-entered quantities/prices, gift card recipients, rental dates, tier prices, tax/currency/native repricing, stock and notification controls, downloads, videos/3D/zoom, estimate shipping, comparison, sharing, related/also-purchased/filter-level compatibility, reviews and all native script/form identities. Description, specifications and reviews are placed in keyboard-accessible HTML details accordions without moving review forms into the add-to-cart form.

Checkout retains addresses, pickup/shipping, payment discovery and payment-plugin forms/redirects, coupons, gift cards, reward points, checkout attributes, terms, tax and shipping totals and both one-page and multi-step native flows. The header uses native search autocomplete, customer links, wishlist/private messages/cart/flyout, currency/language/tax selectors and complete configured menus. The footer uses native Footer so subscriptions, menu trees, social links, tax text, copyright and theme selection remain available. Mobile menus and non-zoom image thumbnails receive keyboard access.

## Validation and limits

The MishaShop Razor library and compiled Tailwind stylesheet build with no errors/warnings. The MVC views require the host's independent assembly references and view-component bridges to compile/render. No fake shopper records, payment transactions or production orders are created by the theme. SMS login is disabled in the MishaShop profile until a real configured integration is available; native login and its enabled authentication capabilities remain accessible. Native/plugin capability preservation is structural; production gateway, shipping, tax and third-party plugin integration still require store-specific end-to-end validation by the host.
