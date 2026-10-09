# Native catalog migration boundary

Architecture and independent implementation review: gpt-6.1-sol, xhigh, 2026-10-09. Implementation, HTTP acceptance and desktop/mobile Luna checks are complete locally. Full-roadmap acceptance remains incomplete.

Use native MVC category/search controllers and prepared models. Product results remain Theme Razor markup outside WebAssembly. Two small BB/WASM islands occupy the separate toolbar and sidebar boundaries; they submit native GET URLs with full navigation. The sidebar also owns its mobile sheet. This keeps SSR, shareable URLs and browser Back without another catalog JSON API or shared client-state framework.

## Native capabilities confirmed from the checked-out source

- Category filtering: price, manufacturer IDs, specification-option IDs, sorting and paging.
- Search: price and one advanced manufacturer (`advs=true&mid=...`). Its factory does not read category-filter `ms` or `specs` parameters.
- `/search` without `q` intentionally does not list products. An empty query only searches with a zero minimum-search-length setting. No search setting will be changed merely to impersonate an all-products route.
- Sorting values: display order `0`, name `5/6`, price `10/11`, newest `15`. There is no native popularity sort.
- There is no native only-in-stock query filter. Do not show a control whose value the server ignores.
- Current sample colors are variant attributes, not filterable specifications. Color facets require real specification mappings, or a separately justified plugin extension that filters before paging.

## Files and data boundary

Theme overrides: category template, Search, `_ProductsInGridOrLines`, and `_ProductBox`. Preserve native metadata, breadcrumbs, descriptions, warnings, totals, pagination and widget positions. Product cards use `ProductOverviewModel` server-formatted prices and native URLs. Any optional brand/category/color-dot enrichment is bounded to the current result page; never query the old 200-product bootstrap.

The plugin maps only already-prepared control models into public UI parameters: native route, sort and size options, available/selected price range, manufacturer options, specification groups and search fields. The client contract has no product list, raw prices or purchasing rules.

Native query keys: `orderby`, `viewmode`, `pagesize`, `pagenumber`, `price`, `ms`, `specs`, and search-specific `q/advs/cid/isc/mid/vid/sid/sit`. Changing a filter resets the page to one. Removing a price bound omits `price`; limits come from the prepared model.

Existing AJAX actions can be adopted later if needed: `/category/products?categoryId=...` and `/product/search` are POST actions returning HTML partials. They require antiforgery and an HTML helper, not the JSON `postForm` helper. Never insert Blazor markers into an AJAX result container and expect hydration.

## Global catalog extension

The all-products destination is a real gap. The follow-up gpt-6.1-sol/xhigh review selected a narrow plugin MVC action and a small composition factory using native SQL-paged `SearchProductsAsync`, product overview preparation and public sorting/view/page-size helpers. No synthetic root category or broad catalog JSON API is needed.

Global facets need a metadata-only query: select distinct manufacturer/specification option IDs from published, nondeleted, individually visible and date-valid products after native ACL/store query filters. Do not materialize the product catalog. Native categories/manufacturers/specification services provide authorized/localized entities. The global route supports combined `q/cid/ms/specs/price` filters; native Search retains its supported single `mid` control.

Only the two protected currency/clamping helpers need a small plugin counterpart. Native price filtering compares stored base `Product.Price`, rather than the final discounted/tax-inclusive display price. Cards continue to use native prepared display prices.

Color uses real filterable specification mappings editable from admin, alongside existing variant attributes. Explicit fixture repair must add mappings to existing MRG products and enable fixture category price filtering; runtime code must honor native filter settings. The unrequested legacy only-in-stock checkbox will be removed because the native query does not support it. Out-of-stock products remain visible. Per-page availability follows the native ProductDetails inventory switch; `DisableBuyButton` and `FormatStockMessageAsync` with empty attributes do not reliably represent aggregate variant stock.

The high-priority home and search replacements have been removed: `/` and `/search` use native controllers and Theme views. `/catalog/all` is the global destination, and `/catalog/search` remains a native search preview. `/stationery/catalog` redirects through a narrow GET action without the 200-product bootstrap. It preserves native query keys and explicit numeric sort order; category-only legacy links use authorized native slugs, while category-plus-query links use global `cid`. Missing or ambiguous category names return 404. Existing saved menu URLs are preserved. Product/cart/login compatibility routes and their old APIs remain for the next sections.

Validate pagination beyond 200 products, multi-category membership, combined filters, native totals/prices/order, browser Back and query reload, disabled facets, actual color specifications, and desktop/mobile BB/WASM controls without a new server circuit. TUnit covers HTTP behavior; Luna/computer-use provides visual and interaction evidence.

## Implemented data and settings

`NativeCatalogModelFactory` fills only the all-products gap using native SQL paging, native overview models and public sorting/view/page-size helpers. Facet discovery selects distinct mapping IDs after ACL, store, publication and availability checks; it does not load the catalog into memory. Sorting choices reflect the actual configured native options. The optional native canonical setting and Continue shopping attribute are honored.

The guarded administrator fixture operation adds real filterable color specifications alongside the existing variant selections, with per-product/category markers preserving later edits. It enables fixture category price filters once, inserts only missing Persian catalog resources, and converts inherited untouched installer search bounds (0–10000) into a store-specific automatic range. All three possible explicit store range settings are protected, and the marker prevents later repair from resetting an administrator edit. Product prices, inventory, variants and currency are preserved. Runtime code reads the administrator settings; it does not repair data during a catalog request.

Native search exposes its supported advanced category and single-brand controls; it does not pretend to support the category factory's `ms/specs` facets. Disabled native category facets hide their controls. The core category factory still accepts manually supplied `ms/specs` query parameters when those controls are disabled; that native behavior was preserved. The global extension ignores disabled facet query values. Equal-price or single-product ranges disable price filtering, matching native search. Price input supports ASCII digits, the active decimal separator and invariant comma grouping; full Persian-digit/grouping input is not implemented.

## Validation evidence

- Full opt-in TUnit run: 26 passed, zero failed/skipped, 34.202 seconds. The corrected settings-cleanup path was rerun separately afterward: one passed, 1.231 seconds. The full-run TRX remains in `src/Tests/Nop.Storefront.Tests/bin/Debug/net10.0/TestResults`.
- The 201-product test creates two categories and 201 products through native administrator forms, checks the last page, reverse price order, the last product's exact SKU and canonical destination, two category memberships, and combined category/query/price parity with native Search. Product deletion is batched through native admin; the test verifies no public records remain under its unique prefix.
- The settings test temporarily disables manufacturer/specification/price facets using native admin endpoints, verifies global and category controls disappear and global query values cannot keep filtering, then restores every original setting in `finally`. No direct database mutation is used.
- Main-route tests verify native SSR at `/`, Search parity at `/search`, legacy sort/query preservation, authorized category redirects and unavailable-category rejection. Invalid category/selectors and price-range parity are also covered.
- Luna captured inline screenshots at 1440×900 and 390×844. Sort, page size, list/grid mode, reload/Back, real brand/color/category filters and reset worked. Mobile document width was 375px in the 390px viewport, without horizontal overflow.
- All-products price bounds came from the actual fixture: 65000–680000. Applying `125,000` normalized the URL to `price=65000-125000` and returned the two matching products. Search for `دفتر` showed two products with bounds 185000–245000; a maximum below the minimum returned no results, and reset restored both products.
- Sol found and verified the selected-sort, view-mode, localized-route, reset vendor/scope, decimal separator, explicit Theme partial lookup, store-setting preservation and test-cleanup fixes. Plugin/client build passed with zero warnings/errors, and official Microsoft formatting plus `git diff --check` passed.

Luna initially found that BB's sheet focus trap cleaned up without restoring focus. The opener is now an explicit `AsChild` BB trigger, and the component uses BB's own `IFocusManager.FocusFirst` after close. In the final 390×844 check, both the close button and Escape returned focus to the same real opener after a fresh DOM read. Header search for `دفتر` submitted to `/search`, retained two products after reload, and mobile Shop navigation reached `/catalog/all` with eight products. The homepage hero/categories/navigation remained visually intact. Final desktop and mobile screenshots were captured inline; no standalone screenshot file is claimed. Cart/OTP cleanup, widget demonstrations, Docker/publish and performance acceptance are still outstanding.
