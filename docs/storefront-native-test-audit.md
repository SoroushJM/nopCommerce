# Native storefront integration test audit

Source audit: 2026-10-12. No HTTP integration test, database mutation, server start, or request to port 5090 was performed for this audit. Historical pass counts in the tests README do not establish compatibility with the new Boutique theme or current independent packages.

Validation for the guard change: the integration test project builds successfully (one existing CS0108 warning in the generated Europa VAT service reference); the 23 pure `StorefrontTestTargetTests` cases pass with zero failures or skips. These tests access no server or database.

## Isolation contract

`StorefrontClient` now requires `STOREFRONT_TEST_URL` and `STOREFRONT_TEST_ISOLATED=1`. Only HTTP loopback hosts with a non-default explicit port are accepted; port 5090 is always rejected, including IPv4 and IPv6 spellings. Credentials, query strings, and fragments in the base URL are rejected. A trailing slash is normalized to preserve a path base. Each outgoing request is checked against the configured scheme, host, and port before reaching the transport; absolute links cannot escape to a different server. Automatic redirects remain disabled.

Ports 5202 and 5230 were the initial independent QA endpoints. The later package clones use 6240 and 6250; see [the current completion audit](qa/storefront-completion-audit.md) for health and fixture status before running tests. No port is a hard-coded requirement beyond the 5090 rejection. The isolation environment flag is an operator declaration, not proof of the server's database connection. Verify the effective server database configuration independently before running HTTP tests. Do not run both theme suites against the same database concurrently: `NotInParallel("storefront-http")` serializes inside a test process, not across two processes.

```powershell
$env:STOREFRONT_TEST_ISOLATED = '1'
$env:STOREFRONT_TEST_URL = 'http://127.0.0.1:6240/'
$env:STOREFRONT_TEST_PRODUCT_ID = '<actual fixture product ID in this database copy>'
dotnet run --project src/Tests/Nop.Storefront.Tests -- --treenode-filter '/*/*/NativeProductTests/*' --report-trx
```

Run only after the isolated server is ready. Change the URL for the other theme in a separate process. The configuration guard itself can be tested without any server or environment configuration:

```powershell
dotnet run --project src/Tests/Nop.Storefront.Tests -- --treenode-filter '/*/*/StorefrontTestTargetTests/*' --report-trx
```

## Existing coverage and suitability

| Test class / scenario | Useful behavior | Prerequisites and limitations |
|---|---|---|
| `NativeProductTests` selection, missing CSRF, unavailable selection, guest add/edit/merge | Native attribute quote, stock rejection, antiforgery, persistent cart, merge ownership | No credentials. Product ID must have at least two in-stock combinations and one out-of-stock combination. SSR/WASM and restored-selection HTML assertions describe the former Shared product component; native Boutique control markup may require theme-aware assertions. |
| `NativeCartTests` | Native quantity update/removal, invalid quantity, ownership, CSRF, formatted amounts, coupon response | No credentials; same combinational product fixture. Cleanup in `finally`. Full cart DOM assertions depend on the plugin's `data-cart-itemid` and subtotal hooks; validate actual theme output first. |
| `NativeCartAllowedQuantityTests` | Stock rejected quantity preserves saved quantity, amount, selection | Admin credentials and fixture gate. Creates its own product, clears guest cart, deletes product in nested `finally`. Good bounded functional scenario after the admin gate issue is resolved. |
| `NativeCheckoutAttributesTests` | Checkbox/date/text/file/readonly cart selections survive quantity updates | Admin gate and an existing purchasable product fixture. Clears guest cart and deletes created attributes; uploaded Download record remains. It does not execute checkout steps or place an order. |
| `NativeCartSettingsTests` | Mini-cart cap, images, enabled state, checkout availability | Admin gate. Snapshots original global settings and restores each independently, then clears cart; cleanup errors aggregate. Needs the exact expected setting rows and native cart adapter contract. |
| `NativeProductConditionTests` | Conditional attribute parsing and cart edit restoration | Admin gate. Creates product, attributes and mappings; independently deletes owned resources and cart, aggregates errors. Initial/restore HTML assertions assume Shared suppresses inactive controls server-side. Boutique preserves native conditional controls and JavaScript, so unchanged assertions cannot establish its behavior. |
| `NativeProductPresentationTests` / `NativeProductCombinationPresentationTests` | Gallery, tier price, minimum/stock/maximum, metadata, combination images, base price | Admin gate. Upload helper still reads `/_content/Storefront.UI/images/` assets; the extracted/independent packages use different paths and Boutique lacks those stationery images. Assertions use `sf-product-*`, `sf-price-card`, Shared parameter JSON and Persian labels. Do not run unchanged and call failures native regressions. |
| `NativeProductPolicyTests` | Discount/CallForPrice, guest price permission, advanced native purchase forms after invalid review | Admin gate. Global permission/settings snapshot restored; owned products/attributes/discounts removed. Advanced fallback fields useful for both themes, but price-card assertions are Shared-specific. |
| `StorefrontApiTests` OTP and direct login | Mock authentication, guest cart migration, cooldown/ownership | OTP requires Development test-code mode and creates persistent mock customer records. Boutique explicitly uses native login with OTP disabled; these scenarios are not Boutique feature criteria. |
| `NativeFixtureRepairTests` | Development presentation repair idempotence | Deliberately changes seeded data; unsuitable as a neutral independent-package smoke test. |
| Catalog/admin scale tests | Search facets, paging, settings, menu/category management | Valuable separately. The scale test creates 201 products; prioritize bounded scenarios first. |

## Administrator credentials and blocked gate

`LocalAdminFixture` is skipped unless `STOREFRONT_TEST_ADMIN_FIXTURE=1`. It reads an external JSON file from `STOREFRONT_TEST_PRIVATE_SETTINGS`, or the existing LocalApplicationData `MadadrangDev/local-settings.json` location. Required keys are `Database`, `AdminEmail`, and `AdminPassword`; do not print the file or values. Native `/login?returnurl=%2FAdmin` authenticates with those credentials, then `/Admin/PersianStorefrontMaintenance/Configure` must expose the unique `madadrang-fixture-repair` marker before mutations proceed.

The current helper accepts only `Database == "nop_stationery_dev"`. The server's maintenance controller independently enables that form only for a loopback PostgreSQL connection whose effective database name is exactly `nop_stationery_dev`, in Development with a loopback request. Independent QA database copies with their own names therefore cannot pass this gate unchanged. Updating only the private JSON database label does not solve the server-side check. This audit does not weaken or bypass that gate; the parent must choose and validate a dedicated isolated-copy gate before opting into administrative tests.

## Cleanup is bounded, not a complete database reset

Cart cleanup parses item IDs from full cart HTML, posts native remove fields through the adapter and checks JSON `Count == 0`. It does not delete guest customers, their generic attributes, or already-uploaded Download records. Product deletion through nop's administration is soft deletion; pictures/combination associations are explicitly removed in some presentation tests. A failure before the created ID is recorded, abrupt process termination, or failed cleanup can leave owned resources. Restore or discard the disposable QA database copies after a run if a pristine baseline is required.

The condition and combination tests aggregate independent cleanup failures. Other tests have nested `finally` blocks, but some multiple-cart cleanups are sequential: failure of the first `ClearCartAsync` can prevent the second. Settings restoration is attempted, not transactional. The test report must include cleanup failures; a HTTP redirect or success alone is not proof of every residual row being removed.

## Missing full checkout coverage

No existing test in `Nop.Storefront.Tests` submits native billing/shipping addresses, shipping method, payment method/information, confirm order, or order completion. `NativeCheckoutAttributesTests` covers shopping-cart checkout attributes only. Browser/native HTTP QA for both themes must independently exercise address validation, shipping/payment plugin forms, terms, confirm, order completion and order/account visibility using disposable copies and a configured non-external payment method. That requires a dedicated test customer, valid configured addresses/country/state, a shippable product where appropriate, configured shipping and offline/test payment plugins; administrator credentials alone do not supply those prerequisites.
