# Storefront feature coverage and validation boundaries

Source review date: 2026-10-12. Baseline: nopCommerce 5.00 / net10.0.

This document records how the independent Boutique and Madadrang interfaces keep existing nopCommerce capabilities available. It distinguishes source preservation from runtime verification. The requirement is to preserve and complete the UI of existing capabilities, rather than add new commerce behavior. The clothing choices come from `../shared-chat-full.json`; the user-approved turns are the authority.

## Approved Boutique presentation

| Decision | Approved choice | Implementation |
| --- | --- | --- |
| Palette and atmosphere | Cream, nude and ivory; soft women's boutique | `Storefront.UI.Boutique/Styles/input.css` |
| Brand | Use the store's configured name/logo; final brand name not supplied | `BoutiqueHeader`, root `StorefrontThemeService` and configured `LogoPictureId` |
| Hero | Split copy/photo, real store photography | `BoutiqueHero`, root `BoutiqueThemeSettings` and picture service |
| Categories | Round cream pills | `HomepageCategories/Default.cshtml`, native category routes |
| Mobile products | Two products per row | Native product grid styled with two columns below 640px |
| Gallery | Main photo plus thumbnails | Native `_ProductDetailsPictures`, zoom/video/3D retained |
| Size/color | Minimal size options and round colors | Native attribute form; scoped option-list and ColorSquares styling |
| Mobile add-to-cart | Within product information | Native `_AddToCart` remains inline; no sticky bar |
| Additional information | Minimal accordions | HTML details around native description/specifications/reviews |
| Cart | Compact and orderly | Native cart/order summary with scoped table/totals styling |
| Checkout | Separate rounded cards | Native one-page and multi-step checkout sections styled |
| Login | Minimal, no clothing photo | Native login page centered; authentication controls retained |
| Customer orders | No final visual option chosen | Native order interface styled by the common layout |

No fixture products, invented prices, stock clothing pictures or invented Boutique brand are part of these templates. Missing hero photography yields a neutral slot. Hero setup help is conditional on the server's `ShowSetupHint`; the bridge must enable it only for an administrator and only when no configured photo exists.

## Capability map

Paths in this table are relative to `src/Presentation/Nop.Web/Views` unless a theme/plugin is named. The same configured native settings, permissions, business services and installed plugins continue to decide whether a capability is enabled.

| Existing capability | Boutique UI route and preservation | Madadrang/shared UI route and preservation |
| --- | --- | --- |
| Category navigation, featured items, descriptions, breadcrumbs, SEO/JSON-LD | Themed `Catalog/CategoryTemplate.ProductsInGridOrLines.cshtml` copies the full native view | Theme category/native catalog bridges; native fallback continues under common layouts |
| Search: term, category/children, manufacturer, vendor, descriptions, tags | Themed `Catalog/Search.cshtml` keeps all native advanced fields and scripts | Native search route remains available; custom all-products view provides scoped browsing |
| Price, specification and manufacturer filters | Native `_CatalogFilters` and filter-box partials | Shared catalog controls use native-prepared filter model and links |
| Sorting, page size, grid/list, pagination and AJAX | Native `_CatalogSelectors`, `_CatalogProducts`, `_ProductsInGridOrLines` | Shared/native catalog implementations; full native routes remain available |
| Entire-catalog browsing without a search term | Root AllProducts controller with native `SearchModel`, `UseAjaxLoading=false`; advanced form submits to native SEARCH | Same factory/controller, theme-selected view |
| Product SKU, ratings, tax/shipping/base-price information, buy/rental/preorder, wishlist/compare | Native `_ProductBox` copied with all permitted actions and widget zones | Native-prepared product cards plus native product routes |
| Product gallery, native zoom, videos and 3D | Full native picture/video/3D partials | Shared `NativeProductPage` for supported models; native product fallback for unsupported models |
| Every enabled product attribute type, conditional attributes and combinations | Full native `_ProductAttributes` including its dynamic scripts and API routes | `NativeProductPresentation.Supports` determines when the shared model is safe; full native fallback must remain active |
| Attribute file uploads, quantity per option, text/date, customer-entered price, gift cards, rental dates | Original field prefixes, forms and partials retained in Simple/Grouped templates | Native fallback rather than silently omitting controls the shared model cannot represent |
| Stock, delivery dates, notifications, tier pricing and native repricing | Original availability/delivery/attribute/price/add-to-cart partials retained | Native/shared preparation; fall back where coverage is incomplete |
| Related/also-purchased, tags, specifications, manufacturers, filter-level compatibility | Original components and partials retained | Native supporting components and fallback |
| Reviews, helpfulness and review validation | Original review form and routes retained, outside the add-to-cart form; accordion presentation | Native views and supporting shared product content |
| Cart and wishlist operations, quantities, move/share/custom wishlists, compare | Native `ShoppingCart`, wishlist and comparison views through Boutique layouts | Shared mini-cart plus full native cart/wishlist/compare routes and links |
| Cart attributes, discounts, coupons, gift cards, rewards, shipping estimates, server-calculated taxes/totals | Native OrderSummary and all native partials; no browser reimplementation of prices | Native cart summary retained as authoritative; shared mini-cart is an entry point |
| One-page and multi-step checkout, addresses, pickup points, shipping/payment discovery | Native `Checkout/*` and plugin payment views; original form names/actions/scripts | Native checkout routes retained |
| Payment plugin forms, redirect/callback and order placement | Native processing and plugin UI remain responsible | Native processing and plugin UI remain responsible |
| Native login/registration, password recovery, MFA and external authentication | Native customer views; Boutique OTP disabled until configured real integration exists | Native routes remain accessible even when development OTP UI is available |
| Profile/customer fields, addresses, consent/privacy, password change | Native Customer views and account-navigation component | Native customer routes and configured account menu links |
| Order history/details, invoice/print, downloads, returns, rewards and recurring orders | Native Order/Customer views through common layouts | Native routes remain accessible through native header/footer/account menus |
| Currency/language/tax display and configured store logo | Native selector components and configured logo URL | Root adds native selectors/logo to Madadrang header |
| Private messages, wishlist, customer/logout/cart links, native flyout cart | Native HeaderLinks and FlyoutShoppingCart, original header-upper hook | Root adds native HeaderLinks/Flyout alongside shared header entry points |
| Nested/list/grid menus and configured destinations | Native MainMenu and item partials; mobile toggle is a button | Root uses native MainMenu where configured item templates require it; shared menu remains for supported presentation |
| Newsletter subscribe/unsubscribe, CAPTCHA, social links, footer menu trees, copyright/tax text, theme selection | Native Footer, NewsLetterBox, FooterMenu and social components | Root NativeFooter now includes the native Footer component |
| Topics/contact, sitemap, vendors, blogs/news/forums and other native public pages | Theme `_Root`, `_ColumnsOne`, `_ColumnsTwo` style native fallback views | Native pages remain available; common layout/native controls must stay loaded |
| Plugin widget zones, cookie consent, admin link, validation/messages, antiforgery, configured custom head/footer | Full native root/head pipeline and original hooks retained | Existing native root/head pipeline plus native header/footer additions |

## Static audit evidence

Boutique copies nine core native files: Category, Search, product grid partial, product box, Simple/Grouped product templates, picture gallery and both column layouts. A source comparison of their native widget-zone references, partial/component calls and form field/ID references found **zero missing entries**. This checks preservation of source interfaces; it does not prove every configured extension renders or works at runtime.

The root/head chain retains `BodyStartHtmlTagAfter`, `ContentBefore/After`, `BodyEndHtmlTagBefore`, native notifications, JavaScript-disabled warning, admin links and cookie consent. The themed column layouts preserve `MainColumnBefore/After`, `LeftSideColumnBefore/After`, `LeftSideColumnAfterCategoryNavigation`, `Breadcrumb`, `left` and `CatalogFilters`. Boutique header retains `HeaderBefore/After`, `HeaderMiddle`, `HeaderSelectors`; HeaderLinks itself provides the native header-link widget zones. Native footer supplies its Footer zone once.

`NopHtmlHelper.AppendCssFileParts` inserts at index zero, but source order alone does not establish precedence over Tailwind's named layers: an unlayered native reset overrode layered spacing utilities in the earlier preview. Both Head partials now emit a direct versioned native-wrapper link; the wrapper imports DefaultClean into the `native` layer between `base` and component/utility layers. The native bundling link helper is removed only in those two partials, preserving the standard version helper and other registrations. See [the cascade regression and evidence](qa/storefront-native-css-layer.md). Computed styles and appearance of the corrected package still require browser verification. Extension styles can continue to override theme rules according to their cascade position.

Three concrete compatibility fixes were made after this audit:

1. Native ColorSquares markup uses `.attribute-square-container` / `.attribute-square`, not the earlier guessed `.color-container` / `.color`. The theme now targets actual native names, makes only ColorSquares round, and shows selection through both the native `.selected-value` class and `input:checked`. Native important square dimensions are overridden only inside ColorSquares; image-square and disabled-variant rules remain available.
2. Native newsletter optional unsubscribe controls and CAPTCHA are children of `.newsletter-email`. The theme's container now wraps; options/CAPTCHA receive full rows, the email field can shrink, and CAPTCHA can scroll horizontally if its external widget is wider than the available space. The original input IDs, routes, checkbox/radio values and CAPTCHA behavior are unchanged.
3. Product essential widget output can appear beside the gallery/overview as direct children. Those non-gallery/non-overview children now span the full grid width. The two main product columns keep their intended layout.

## Verification performed and remaining runtime checks

Performed by the Boutique implementation task: independent Razor library build with zero warnings/errors; pinned Tailwind CSS regeneration; JavaScript syntax check; static source/field/widget comparison and native stylesheet order analysis. No Boutique real-store runtime or payment test is claimed here. Host build, browser snapshots and published-package evidence are recorded separately by the integration task.

Required store-specific verification includes desktop/mobile product gallery and variants; stock/conditional combinations; file/gift/rental product forms; filter/sort/page state; registered/guest cart and merge; wishlist/compare; account registration/MFA/recovery and privacy; both checkout modes; enabled pickup/shipping/payment plugins and their redirects/callbacks; coupon/gift/reward/tax totals; customer order/download/return operations; newsletter CAPTCHA/unsubscribe; and configured widget/menu/plugin content. These checks must use actual configured data and services. A native fallback guarantees that the original UI is reachable, but source preservation alone cannot certify a particular shipping, payment or third-party plugin integration.

Release isolation is a separate packaging concern: each published store chooses one theme UI assembly/client asset set and its own store/database configuration. This feature map does not authorize deployment or represent proof that a release artifact contains only its selected theme.
