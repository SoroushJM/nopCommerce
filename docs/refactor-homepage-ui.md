# Native homepage and editable navigation acceptance

Date: 2026-10-09. Branch: `refactor/native-theme-client-ui`. Preview: `http://localhost:5090/home/index`.

## Implemented scope

The standard `HomeController.Index` now uses the Madadrang Theme homepage. The existing hero, colorful category tiles, product grids, banners and footer retain their accepted layout. Homepage categories and featured products use the core ViewComponents; the four new products use the core marked-as-new query and product overview factory. Card prices are server-formatted native prices, and links/pictures come from native models. Enrichment reads only the displayed products, not the legacy 200-product bootstrap.

Tiles use native category IDs and prepared SEO links. Four initial icon/color styles are development fixture attributes keyed by category ID; an additional category gets a safe generic style. Banner destinations are stored category IDs and resolved through publication, ACL and store checks, so runtime links do not depend on the original category names.

The native header now has static brand/search structure and separate small WASM islands at widget boundaries. Header-links widgets receive the native model around the account/cart controls; middle, desktop menu and mobile menu zones have their own positions. The announcement is outside the header, matching the accepted UI. One portal host serves the menu/cart sheets and account dialog. The footer uses native menus with the original grid and plain root-link spacing.

Native homepage, body, header, footer and product-card widget calls remain. Notifications, the administration header link and the cookie-law component are retained. A product-card view falls back to the original nop view outside the migrated native-page layout.

The accepted `/` and legacy catalog/search/cart routes remain unchanged until their replacement sections pass acceptance. The new page is a native preview; this document does not claim the complete roadmap or route migration is finished.

## Explicit local fixture repair

The plugin configuration page exposes an administrator operation only in Development, with a loopback request/remote address, the effective PostgreSQL configuration, and the exact loopback `nop_stationery_dev` database. Plugin-management permission and antiforgery are required. The guarded form has a unique marker so administrative tests cannot mistake unrelated admin maintenance buttons for permission to modify fixtures.

Repair adds missing native slugs, sets sample homepage/new-product flags, records presentation category IDs and inserts owned editable menus. Existing owned-menu edits are retained. Only complete untouched installer-menu fingerprints are unpublished; localized changes are also preserved. The operation does not modify prices, inventory, combinations, pictures, currency or checkout settings.

Luna found the installer English `HomepageText` between the hero and categories. Repair now unpublishes that exact untouched installer text once. Its title/body, publication flags, schedules, metadata, translations and native Default-template identity are checked; customized topics remain available. A handled marker preserves a later deliberate republication. The standard TopicBlock call remains available for administrator content.

## Independent review

A gpt-6.1-sol/xhigh architect specified the native homepage/fixture/header split. A fresh gpt-6.1-sol/xhigh reviewer found and verified fixes for nullable menu strings, ambiguous multi-category fixtures, localized menu edits, the administrative test guard and topic customization preservation. No core or admin-design files were changed.

## Luna computer-use evidence

- Desktop 1440×900: right-aligned all-products/category links and far-left offer match the accepted layout. Eight product cards and all 11 content images loaded. Document width and scroll width were both 1425.
- Mobile 390×844: document width and scroll width were both 375. Hero stacking and fixed bottom navigation fit the viewport. Category/cart/account each opened a single dialog and closed correctly.
- Native product 1: blue is available; orange makes purchasing unavailable. Adding two blue items opens one cart sheet, updates the badge to two and displays 370,000 تومان. Removal restores the empty cart and zero badge.
- After the English-topic correction, desktop and mobile both showed no installer welcome heading. Category tiles started 24px after the hero in both viewports.
- The actual `/home/index` footer has two native menu groups with five and three links. Desktop brand/menu columns fit with 32px gaps and 28px link spacing.
- Screenshots were captured inline through documented computer-use. No standalone PNG file is claimed. The browser account was retained; no logout or OTP submission was performed by visual QA.

## Automated validation

The final TUnit run passed all 21 scenarios with zero failures or skips. It covers native SSR/CSRF, product/category destinations, served pictures, anonymous repair denial, repair idempotence, administrator category create/rename/unpublish, and nested/unpublished native menu items. The added fifth category and extra footer menu appeared without a build and were removed through native admin afterward. Post-cleanup, the homepage has four category tiles, eight product cards and two footer groups.

Administrative mutations require `STOREFRONT_TEST_ADMIN_FIXTURE=1`, private credentials outside Git and confirmation of the server's unique guarded form. Without this opt-in, the three admin fixture tests are skipped; the other 18 scenarios remain available. Upstream NUnit tests are untouched.

Plugin/browser-client builds and Tailwind generation passed. Microsoft tools formatted C#/stable Razor files; details and the two Razor convergence limitations are in [storefront-formatting.md](storefront-formatting.md). An upstream `CS0108` warning in the generated VAT service appeared during a dependency rebuild; it is unrelated to the storefront changes.

Pending full-roadmap acceptance includes the native catalog/filter migration, full cart/OTP cleanup, published/Docker assets, plugin widget demonstrations and performance measurements. Native category destinations currently use the preserved default Theme fallback pending the catalog section.
