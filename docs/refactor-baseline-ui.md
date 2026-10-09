# Baseline UI check

Date: 2026-10-09. Target: local storefront at `http://localhost:5090`.

Method: visible Chrome task tab through the documented `mcp__cua_repl` browser surface; all interaction checks used the rendered UI.

## Viewports and visual captures

- Desktop override: 1440 × 900 CSS px (`window.innerWidth`/`innerHeight`); screenshot content was 1425 × 900 px. The 15 px difference is the browser viewport scrollbar area. Home and product pages rendered in RTL with loaded product/hero photography and no horizontal overflow.
- Mobile override: 390 × 844 CSS px. The document client width and scroll width matched on the home and product pages, so neither page had horizontal overflow. The screenshot API returned 375 px wide images (home 375 × 844; product/cart 375 × 812); the page itself reported `innerWidth=390` and `innerHeight=844`.
- Screenshots were captured and visually inspected for desktop home, desktop product, mobile home, mobile product, mobile catalog, and the mobile cart drawer. They appeared inline in the Computer Use results. The documented browser API returns screenshot bytes to its REPL but exposes no filesystem write operation, so I could not save copies under `docs/qa/refactor-baseline/` without using an undocumented mechanism.

## Interaction checks

- Product `/stationery/product/1`: selecting green updates the displayed unit price from 185,000 to 195,000 تومان. Selecting the unavailable orange option shows its unavailable message and disables the purchase button. Selecting blue restores availability.
- Cart: adding the notebook opens the cart drawer with one item and total 185,000 تومان. Increasing quantity to 2 updates total to 370,000; decreasing returns it to one, and remove returns the cart to its empty state. Repeated once at mobile size; the mobile drawer displayed the item and total. The test item was removed after the checks.
- Catalog `/stationery/catalog`: search for `مداد رنگی` returns exactly the colored-pencils product. The brand dropdown filters to Papco products; combining Papco with the “up to 200,000 تومان” filter leaves two results. Clearing those filters restores all eight products. The custom brand/price/color dropdowns opened and exposed selectable options.
- Login dialog: opening the account control showed an existing signed-in mock session. No account data was entered, no login was submitted, and no sign-out action was taken.
- Browser console: no error-level entries were returned during the check.

## Baseline notes

The desktop and mobile pages keep the expected colorful stationery styling, RTL reading order, product images, and mobile bottom navigation. The product color choice, stock state, pricing, and cart actions worked in the rendered UI. This check records the current implementation before the refactor; it does not verify native nopCommerce theme/model integration or server-load improvements.
