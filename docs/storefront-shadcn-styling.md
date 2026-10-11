# Storefront styling with shadcn conventions

Implemented locally on `refactor/native-theme-client-ui`, 2026-10-10.

The public Madadrang storefront follows the shadcn skill's semantic theme and composition conventions using BlazorBlueprint 4.1.0 and Tailwind 4.1.18. The implementation stays in the existing Blazor UI library, plugin presentation views and native theme. React, a second component framework and a synthetic `components.json` were not added.

## Central theme

`src/Storefront/Storefront.UI/Styles/input.css` defines background/foreground pairs for the page, cards, popovers, primary/secondary actions, muted text, accents and status messages. Art, stationery, school and surface roles retain the colorful store identity. Authored public markup uses semantic utilities instead of raw Tailwind color palettes. Actual product and specification swatch colors remain data from nopCommerce.

Change the root token values to change the palette. Tailwind's `@theme inline` maps these variables to utilities; BB consumes the same variables. Radius utilities use the BB/shadcn multiplier scale around `--radius`. Vazirmatn, RTL and the public layout remain supported. The explicit cascade layer order puts consumer layout utilities after BB utilities, independent of which stylesheet is loaded first. Card shadows use BB's supported `--bb-card-shadow` variable.

BB controls use their own variants, typography, focus and disabled states. Primary actions use the Large variant size (44px). Inputs/selects and native control adapters use a matching 44px control height. BB soft stock badges use the library's built-in status palette; those internal badge colors are not claimed to follow the additional custom status tokens. Alert status aliases do follow the shared theme.

## Component composition

- Header search uses BB InputGroupInput, InputGroupAddon and InputGroupButton inside the existing native GET search form.
- Filter and login forms use FieldGroup, Field, FieldSet and FieldLegend. Price errors retain a live alert and input descriptions.
- Select items are grouped. StoreSelect explicitly resolves initial option labels before the popup mounts.
- Product imagery and purchase information use Card headers/content/footer with accessible titles. Buttons use built-in variants and data-icon attributes.
- Client warnings use Alert, the mini-cart and empty catalog use Empty, and mini-cart totals use Separator.
- The theme renders catalog Empty/Alert through a small plugin ViewComponent, since Nop.Web theme views do not reference the BB assembly directly.

Native MVC form inputs, submit names, widget zones and JavaScript hooks remain in place. Their CSS adapters use the shared tokens and accessible focus/disabled states. Native cart item warnings preserve their existing HTML rendering. Manual overlay z-index overrides and `!important` storefront overrides were removed. Unused notice/empty-state CSS and remaining references to the deleted sf-button class were cleaned up; native cart actions have a scoped adapter.

## References and review

The shadcn CLI was used for project information, registry search, component documentation and viewing registry source. This is a manual Blazor project, so component installation and React initialization are inapplicable. Official references include [theming](https://ui.shadcn.com/docs/theming) and [Field](https://ui.shadcn.com/docs/components/base/field), [InputGroup](https://ui.shadcn.com/docs/components/base/input-group), [Card](https://ui.shadcn.com/docs/components/base/card), [Select](https://ui.shadcn.com/docs/components/base/select), [Alert](https://ui.shadcn.com/docs/components/base/alert) and [Empty](https://ui.shadcn.com/docs/components/base/empty). APIs and cascade behavior were checked against the existing BB source and package.

A fresh gpt-6.1-sol/xhigh agent planned the architecture, then a separate fresh agent reviewed the diff. Review fixes restored two malformed white-space declarations, initial Select display labels, live price-error announcements, purchase-card top padding, valid cart-count sizing utilities and sufficient muted-text contrast. A follow-up caught an unsupported BbAlert Description parameter; StorefrontWarning now composes the description as child content. The final source review found no remaining high or medium priority defects.

## Validation and remaining manual checks

The UI and tests projects build successfully. Full plugin build and client publication pass with `-p:UseRazorSourceGenerator=false`. The default source-generator build currently reports errors in unchanged upstream `_RentalInfo.cshtml` and `CurrencySelector/Default.cshtml`; this task did not change those core files or persist a build setting. Microsoft SDK whitespace formatting was applied and verified for the changed TUnit helper and new ViewComponent. Razor markup was not claimed to have been formatted through a browser or editor session.

The final guarded local TUnit suite passed 42/42 with zero failures/skips on `nop_stationery_dev`, in 37.362 seconds, on 2026-10-11. Four focused native cart tests also passed after the last native button/warning adapter changes. The product-policy test helper now extracts a balanced purchase-card div, accommodating CardFooter without weakening price/permission assertions. A new regression test verifies that a short native search renders nonempty validation text in the catalog alert. Its initial selector matched a shared hidden notification container; scoping to catalog results corrected the test, and both the focused run and final full suite passed. Token contrast calculation checked 21 text/background pairs at or above 4.5:1; muted text on secondary and stationery surfaces measured 4.71:1 and 4.77:1. Source scans found no remaining public raw palette classes, malformed variable property names or `!important` declarations. Git whitespace checks passed.

At the user's request, no Luna, computer-use or browser visual checks were performed. HTTP tests and contrast calculations do not verify layout or client interactions. Manual acceptance remains: desktop/mobile overflow, filter labels before opening, keyboard focus, login and cart sheets, product image dialog, quantity buttons and color choices. Work is local only, without push/deployment/server changes. This styling task does not complete the whole refactor roadmap.
