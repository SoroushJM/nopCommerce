# Storefront visual acceptance — 2026-10-13

Visual inspection was performed in the Terra review session against the public deployments. Image pixels are deliberately not included in this report.

## Boutique — `:9012`

- At 1424px the cream/nude hero preserves its split composition. The real clothing-rail image fills the media panel without a person; the balanced Persian heading has two complete lines and no orphan `تو`.
- At 390px the public header is 318px tall and the hero starts at y=332. The document and viewport are both 390px wide.
- The native menu toggle changes `aria-expanded` from `false` to `true`. The closed menu is hidden after its exit transition and its links cannot receive focus. The open menu exposes all eight links in a 464px scrollable area.
- The menu transition uses `max-height`, `opacity`, and `visibility`; with `prefers-reduced-motion: reduce`, its computed duration is `0s`.
- Thirteen public Boutique images load with natural dimensions. The public photo-credit route is linked from the footer. Cards use contained images inside their cream portrait frame, retaining the full garment or accessory.

## Madadrang — `:9011`

- At 1424px the colourful-pencil hero, RTL header, navigation spacing, search, and four category cards form a stable desktop composition.
- At 390px the document and viewport widths are both 390px.
- A fresh 20-second mobile navigation loaded the hero and the first four thumbnail JPEGs with nonzero natural dimensions. Remaining below-fold images retained lazy-load pending state rather than reporting a decode failure. Independent HTTP checks confirmed all eight thumbnail endpoints return JPEG `200` responses.

## Result

No additional static-layout regression was found in these desktop/mobile Home and native-menu paths. Native forms, navigation, and product controls remain present. The subsequent shared Blueprint lifecycle repair passed isolated and deployed acceptance, documented in `storefront-modal-live-review-20261013.md` and resolved DSN-008. Final cookie-prefix releases also passed the small menu/Sheet regression recorded in `storefront-cookie-and-release-review-20261013.md`.
