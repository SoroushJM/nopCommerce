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

Existing AJAX actions can be adopted later if needed: `/category/products?categoryId=...` and `/product/search` return HTML partials. They require an HTML helper, not the JSON `postForm` helper. Never insert Blazor markers into an AJAX result container and expect hydration.

## Remaining decisions and acceptance

The all-products destination is a real gap: a configured root category can provide native subcategory browsing; otherwise a narrow plugin MVC action can prepare native paged catalog models. Do not silently introduce a synthetic root category or a broad JSON catalog API. Global color/availability filtering requires explicit server support before its UI is retained.

Old category-name links should redirect through authorized native category IDs/slugs. Legacy sort names map to native numeric values. Existing home/product/cart compatibility routes remain until their corresponding sections are accepted.

Validate pagination beyond 200 products, multi-category membership, combined filters, native totals/prices/order, browser Back and query reload, disabled facets, actual color specifications, and desktop/mobile BB/WASM controls without a new server circuit. TUnit covers HTTP behavior; Luna/computer-use provides visual and interaction evidence.
