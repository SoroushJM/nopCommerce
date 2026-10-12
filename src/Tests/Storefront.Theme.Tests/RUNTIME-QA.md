# Separate theme runtime verification

Each theme must run from its own package against its own isolated copied database. Runtime tests never fall back to the active installation on port 5090. All runtime cases skip unless a QA URL is explicitly supplied; the cart case additionally requires `STOREFRONT_QA_ISOLATED=1`. The tests create fresh guest cookie sessions, perform no administrative changes, and clean up only their own guest cart lines. They never submit checkout, create an order or invoke a payment provider.

Example PowerShell setup (use the actual host URLs and a published, supported fixture product with stock for quantity 2):

```powershell
$env:STOREFRONT_QA_MADADRANG_URL = 'http://localhost:2020/'
$env:STOREFRONT_QA_BOUTIQUE_URL = 'http://localhost:2030/'
$env:STOREFRONT_QA_MADADRANG_PRODUCT_ID = '1'
$env:STOREFRONT_QA_BOUTIQUE_PRODUCT_ID = '1'
dotnet test src/Tests/Storefront.Theme.Tests/Storefront.Theme.Tests.csproj --filter 'TestCategory=Runtime&TestCategory!=IsolatedCart'
# Only after verifying both URLs use isolated copied QA databases:
$env:STOREFRONT_QA_ISOLATED = '1'
dotnet test src/Tests/Storefront.Theme.Tests/Storefront.Theme.Tests.csproj --filter 'TestCategory=IsolatedCart'
```

`*_PRODUCT_MARKERS` may contain a JSON string array of expected controls for the chosen product, for example `["add-to-wishlist-button","compare-products-button","product-reviews"]`. These assertions should reflect enabled native settings and the specific fixture. `*_FALLBACKS` may contain JSON `[{"productId":2,"markers":["giftcard_2.RecipientName"]}]`. Supply actual gift/rental/file/customer-price/grouped fixture IDs and native HTML markers. No fallback fixture is created by these tests.

## Automated HTTP checks in this project

| Area | Evidence captured on both themes | Limit |
| --- | --- | --- |
| Package/theme isolation | Own theme CSS, opposite/legacy theme CSS absent, correct brand wrapper, actual CSS/shared JS/Blazor boot served with expected media type | Does not execute Blazor or inspect computed styles |
| Header/footer | Native search control, HeaderLinks and AjaxCart initialization, native footer copyright | Currency/language/tax selectors depend on settings and number of configured choices |
| Native navigation | Login password and CSRF, registration page, wishlist/compare/cart reachable | Does not register or authenticate a user |
| Search | q/advs/sid/sit selectable, query booleans restored | Category/vendor choices depend on fixture data; browser interaction still required |
| Supported product | WebAssembly prerender, no server-render marker, native details form, quantity field and CSRF, optional native action/review markers | Does not execute attribute-change requests or browser events |
| Unsupported product | Explicit fixture markers retained in native body and custom product body absent | Requires separately supplied fixture IDs |
| Isolated guest cart | Missing-CSRF add rejected; native add; persistent own line; foreign guest cannot change it; mini quantity update/removal; formatted native count/flyout sections; full native cart totals | Requires normal quantity-1/2 product and visible mini line; does not place an order |

## Browser checks still required before calling native feature coverage complete

Run each row on both independent QA hosts. Use native administrative fixtures only in those copies and record enabled settings, URLs, screenshot/DOM evidence, request result and cleanup.

| Native flow | Required runtime checks |
| --- | --- |
| Theme/navigation/localization | Desktop and mobile menus, nested list and grid pictures; keyboard focus; native search autocomplete; configured logo; language/currency/tax selector changing actual context; newsletter validation/success; social links; theme selector where allowed |
| Catalog | Category/manufacturer/vendor/tag/search/all-products routes; sort/page size/pagination/grid-list; multi-select manufacturer/specification and price filters; advanced category/subcategory/vendor/description/tag search; preserved query state |
| Supported product | Dropdown/radio size and color/image squares; conditional attributes; selection stock and price changes; quantity limits/tier prices; image zoom; add-to-cart/wishlist with selected attributes; compare; native email-a-friend; review validation/captcha/additional ratings when configured |
| Native fallback | Text/multiline/checkbox/read-only/date/file attributes and customer-entered quantity; rental dates; gift recipient/message; customer-entered price; grouped products; wishlist item editing; download samples; back-in-stock subscription; product videos/3D and plugin widget locations |
| Cart | Native and Shared counts/flyout/drawer converge after both native and Shared adds; edit attributes; allowed quantities; disabled removal; item and cart warnings; discounts/gift cards; required checkout attributes including file/date; estimate shipping; tax/discount/gift/reward totals; cross-sell widgets |
| Account | Native email/phone/password login, registration validation, password recovery; profile/address/order/download/recurring/reward/back-in-stock/wishlist/custom-wishlist/private-message pages as enabled; Madadrang OTP only when its provider/theme policy enables it |
| Checkout | On isolated fixtures, traverse guest/registered billing, shipping/address/pickup, shipping method and payment method selection, checkout attribute validation and order review. A deliberate test order requires an explicitly configured offline/test payment flow; never assume a production payment method is safe |
| Independent deployment | Launch each published package without the other theme's assets; verify own selected theme and DB; unsupported native pages keep readable styles; no alternate store's brand/assets leak |

Existing `Nop.Storefront.Tests` provides deeper useful coverage in `NativeProductTests`, `NativeCartTests`, `NativeProductConditionTests`, `NativeProductPolicyTests`, `NativeCheckoutAttributesTests`, and native catalog/cart settings tests. Its `StorefrontClient` defaults to port 5090, uses the old `X-Madadrang-Cart` alias, and several administrative fixtures temporarily change products/settings/menus. Do not run that suite unchanged against the active installation. Reuse the assertions on explicit isolated hosts and preserve each test's cleanup semantics; the new theme tests use the neutral header and have no 5090 fallback.
