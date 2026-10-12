# Deployed shared modal review — 2026-10-13

Terra medium performed the rendered browser checks on public Madadrang at `http://80.75.5.82:9011/`, using the existing owned review browser. Root and deployment maintenance inspected only text, HTTP responses, source and package hashes.

## Actual Madadrang cart Sheet

| Check | Observed deployed behavior |
| --- | --- |
| Normal close | Overlay150ms/content300ms exit motion retained the closed roots as inert; roots unmounted after completion. |
| Focus | Final close restored the cart trigger. |
| Rapid reopen | Reopening60ms into closing produced a non-inert open root, retained body locking and kept Tab focus inside; eventual close restored focus and scrolling. |
| Reduced motion | Entry had no active animations and opacity1; close completed within50ms. |
| Mobile layout | At390px the document did not overflow horizontally. |

The normal page body uses `overflow: hidden auto`; the open modal sets `hidden`. The page's original horizontal-overflow style is not a leftover modal lock. Separate native UI evidence in `madadrang-cart-ui-review-20261013.md` verifies quantity changes and removal, with the owned test cart returned to zero.

## Shared package integrity and scope

Both Linux packages include the immutable local `BlazorBlueprint.Primitives` version `4.1.1-storefront.1`. Root/plugin assembly copies match SHA256 `51CCE5DACBDFEE170B7FA656A941211914A9972FD1D8D1255EE99CA183E32C8E`. The actual packaged and served modal coordinator matches SHA256 `E8B03CE6BBD2CD51B412F8A8D427BB6EF272159DC774676F503557DAB991FE73`; primitive WASM is `BlazorBlueprint.Primitives.z0nsur13cb.wasm`,902933bytes.

Boutique's reviewed public header and catalog use native MVC controls; no shared modal is mounted in those paths. Its modal repair is therefore supported by matching package/served asset integrity and the actual isolated Sheet/JavaScript Dialog harness described in `storefront-modal-candidate-20261013.md`. This is not a claim of an exercised live Boutique modal. The native Boutique menu's own transition and reduced-motion checks are recorded separately in `storefront-style-review-20261013.md` and DSN-007.

The later cookie-prefix rollout changes server cookie names, without changing this primitive DLL, coordinator or brand stylesheet. Its final public asset checks match the above hashes, and the actual post-rollout category/cart Sheet Escape, unmount and focus regression passed, recorded in `storefront-cookie-and-release-review-20261013.md`.
