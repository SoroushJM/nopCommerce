# Storefront cookie and release review — 2026-10-13

## Scope

Final public verification covered the two storefronts sharing one host:

- Madadrang: `http://80.75.5.82:9011/`
- Boutique: `http://80.75.5.82:9012/`

The browser began with cleared cookies. The review did not add or remove cart lines, create orders, register accounts, send SMS, or use an Admin cart write.

## Same-host cookie and antiforgery verification

Each origin was visited within the same browser cookie jar. Its native cart JSON contract was exercised in its own same-origin page context:

1. `GET /cart` with `X-Storefront-Cart: 1` returned JSON **200**, with count **0** and zero lines.
2. The returned native token field, token, cart URL, and checkout fields were used only in that origin's URL-encoded form update request, with `updatecart=1` and `X-Storefront-Cart: 1`.
3. Each `POST` returned JSON **200**, with count **0** and zero lines.

The browser contained separate customer, antiforgery, and culture cookie-name families for `.Nop.madadrang` and `.Nop.boutique`. Private token, cookie-value, cart-URL, and field material is excluded from source-controlled evidence.

Root independently tested a stronger ordering in PowerShell: both GETs issued their tokens before either POST, then each origin's own token was posted using the same fresh cookie jar. Both responses were200 JSON with count0/zero lines. The reusable `scripts/DeployTestServer/Verify-GuestCookieIsolation.ps1` was then executed successfully with that exact ordering. It refuses populated carts, unexpected checkout fields, missing native contracts or actions outside the selected origin, and exposes only statuses/counts/cookie names. External sanitized evidence: `dual-store-deployment-20261012/root-reusable-guest-cookie-verification-20261013.json`.

The separate Python helper verifies both native administrator sessions in one cookie jar and revisits both Admin pages after the second login; all four requests passed200. Its earlier URL-encoded guest-cart request returned404 while the actual browser and PowerShell requests passed. No storefront workaround was introduced for that test-client discrepancy. The shipped Python helper now performs only the proven auth/read-only checks, and the guest antiforgery check uses the executed PowerShell helper.

## Madadrang final UI checks

- The mobile catalog category selector presents one **All** option (`value=0`); its visible trigger is separate from that option.
- The mobile category menu opens in a Sheet. Escape closes it, unmounts the modal parts after its exit, and restores focus to the category trigger.
- The empty mobile cart Sheet opens successfully. Escape closes and unmounts it, and restores focus to the cart trigger.

## Boutique final UI checks

- The mobile native menu opens from `aria-expanded=false` to `true` with eight navigable links, then returns to `false` when closed.
- The 390 px page has no horizontal overflow.
- The catalog sort, pagination, category-route, and search review is recorded separately in `boutique-catalog-review-20261013.md`.

