# Native catalog migration boundary

Architecture review: gpt-6.1-sol, xhigh, 2026-10-09. This section is planned, not implemented. Execution remains local.

Use native MVC category/search controllers and prepared models. Product results remain Theme Razor markup outside the WebAssembly component. One small BB/WASM island controls filtering, sorting and page size; its initial implementation submits native GET URLs with full navigation. This keeps SSR, shareable URLs and browser Back without inventing another catalog JSON API.

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

## Remaining decisions and acceptance

The all-products destination is a real gap. The follow-up gpt-6.1-sol/xhigh review selected a narrow plugin MVC action and a small composition factory using native SQL-paged `SearchProductsAsync`, product overview preparation and public sorting/view/page-size helpers. No synthetic root category or broad catalog JSON API is needed.

Global facets need a metadata-only query: select distinct manufacturer/specification option IDs from published, nondeleted, individually visible and date-valid products after native ACL/store query filters. Do not materialize the product catalog. Native categories/manufacturers/specification services provide authorized/localized entities. The global route supports combined `q/cid/ms/specs/price` filters; native Search retains its supported single `mid` control.

Only the two protected currency/clamping helpers need a small plugin counterpart. Native price filtering compares stored base `Product.Price`, rather than the final discounted/tax-inclusive display price. Cards continue to use native prepared display prices.

Color uses real filterable specification mappings editable from admin, alongside existing variant attributes. Explicit fixture repair must add mappings to existing MRG products and enable fixture category price filtering; runtime code must honor native filter settings. The unrequested legacy only-in-stock checkbox will be removed because the native query does not support it. Out-of-stock products remain visible. Per-page availability follows the native ProductDetails inventory switch; `DisableBuyButton` and `FormatStockMessageAsync` with empty attributes do not reliably represent aggregate variant stock.

Old category-name links should redirect through authorized native category IDs/slugs. Legacy sort names map to native numeric values. Existing home/product/cart compatibility routes remain until their corresponding sections are accepted.

Validate pagination beyond 200 products, multi-category membership, combined filters, native totals/prices/order, browser Back and query reload, disabled facets, actual color specifications, and desktop/mobile BB/WASM controls without a new server circuit. TUnit covers HTTP behavior; Luna/computer-use provides visual and interaction evidence.
