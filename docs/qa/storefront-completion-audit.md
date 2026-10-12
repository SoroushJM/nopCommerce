# Completion audit for the two independent storefronts

Audit date: 2026-10-12. The user goal remains active. This is an evidence map, not a claim of completed runtime acceptance.

## Explicit requirements and current evidence

| Requirement | Current authoritative evidence | Result / remaining work |
| --- | --- | --- |
| Three UI libraries; brands depend only on Shared; Shared has no brand/server references | Actual `.csproj` files under `src/Storefront`; source namespace scan; evaluated Client/Plugin/Nop.Web references for both selections in `theme-build-checks/release-isolation-audit.txt` | Source boundary established; each branch of conditional references was evaluated |
| MVC/server adapters stay in PersianStorefront | Native controllers/model factories in plugin; pure UI contracts in Shared; client references include no Nop server project | Source boundary established |
| Preserve Madadrang appearance and behavior | Prior UI baseline and earlier isolated HTTP/cart checks; current brand components and assets | Incomplete: current desktop/mobile comparison after all extraction changes has not passed |
| Boutique follows final user choices in shared-chat JSON | Feature map: cream/nude, centered logo, split hero, round categories, two mobile columns, native gallery/variants, inline add, accordions, compact cart, checkout cards, native login | Source implementation present; current rendered acceptance and responsive behavior unproven |
| Replaceable brand/real store imagery; no stock clothing models | Store settings/picture adapter; neutral QA store name; original garment-only SVG seed, `scripts/BoutiqueQaSeed/REPORT.txt` | Configurable source and QA sample established; real store photography has not been supplied |
| Preserve all native controls, validation and widget hooks | Nine Boutique native view comparisons; common/native layouts and fallback; `storefront-feature-coverage.md`; new Shared option-label/description audit | Source comparisons support preservation; require runtime enabled-feature checks on both themes |
| Complete supported custom presentation; fallback for unsupported products | Support predicate; native advanced-product templates; previous Madadrang gift/rental HTTP evidence | Further Shared option display correction in progress; Boutique seed unpublishes old IDs2/3, so new fallback fixtures are needed before reusing those checks |
| Native pricing, stock, cart, order, shipping and payment behavior | Server model adapters/native actions; old isolated Madadrang cart checks; Boutique combinations API3583 = six combinations/five stock/one zero | Sample API and earlier cart evidence only; current cart/account/checkout/shipping/payment traversal incomplete |
| Two independent installs/databases/releases/settings/media | Distinct QA databases and clone app paths; selected-only clean packages; eight evaluated Compose configurations; no private configs/keys/opposite brand assets in packages | Isolation source/package checks pass; actual Linux/container rollout and independent upgrade/restart behavior have not been observed |
| Parallel worktrees and incremental integration | Three dedicated worktrees, source commits and integration history; additional delegated QA catalog seeding | Established |
| Build/publish both outputs | Latest completed full Release refresh before follow-up corrections: Head609884 + locale1119, zero errors/warnings, separate package manifests/logs | Follow-up Shared/CSS/embedded-locale corrections need refreshed package evidence |
| Real desktop/mobile checks and repair observed issues | Baseline/historical browser records; native cascade and Head defects found during preview | Incomplete: both current hosts still execute an old Head and return HTTP500 |
| Mock SMS treated as development only | Theme/environment policy and native authentication fallback | Source policy established; current native account traversal not verified |
| Preserve existing user work | Original port5090 untouched by QA restart; private configs excluded; unrelated deploy files remain uncommitted and unchanged by this task | Established for task actions; do not attribute another thread's deployment checks to this goal |
| Architecture/development/deployment/coverage/evidence docs | ADR0003/0004, current development guide, Visual Studio examples, release guide, feature map, native test audit, locale report, seed report | Current guide repaired; runtime evidence gaps remain explicit |

## Authoritative live state at this audit

- Boutique: `http://127.0.0.1:6250/`, process26200 / `dotnet`, start UTC `2026-10-12T16:13:26.9903454Z`. Identity was re-read, not inferred from smoke.json alone.
- Both Home6250 and Home6240 returned HTTP500 with the old `NopLinkTagHelper` stack. The corrected Head has built in the clean packages but has not been observed in these running clones.
- Boutique database `nop_themeqa_boutique_20261012185304` contains products3583–3594 and categories57–61. Product3583 is the supported primary fixture. Its native combinations API returned HTTP200; 13 original SVGs and native thumbnail70 returned200.
- Madadrang database `nop_themeqa_madadrang_20261012185304` retains its separate normal/gift/rental fixtures. Do not apply Boutique seed to it.
- QA schedule tasks are disabled in these disposable copies. This is QA setup, not a production scheduling recommendation.

Automatic approval review rejected the operation preparing a verified process stop/start helper with `blocked by policy`, without a detailed reason. The rejected operation was not retried through another tool or encoding. A human-run transition was provided in chat. Until the existing corrected package runs, browser/HTML/cart checks dependent on the Head cannot supply new evidence. Independent source repairs and package checks can continue.

## Next acceptance work

1. Run the corrected selected packages on their already isolated configurations through a permitted operator action; observe Home200 and correct theme/CSS/Blazor assets.
2. Verify actual layer precedence, RTL, spacing, logo/hero/catalog and product controls on desktop and mobile; save attributable screenshots and overflow/focus evidence for both stores.
3. Run current theme HTTP cases with Madadrang product1 and Boutique product3583. Supply actual published advanced-product fixtures per store; do not reuse Boutique IDs2/3.
4. Exercise native navigation/search/filter/sort/paging, option price and stock changes, cart/wishlist/compare, validation and configured extension hooks. Include guest/authenticated cart ownership and merge where enabled.
5. Prepare bounded isolated native checkout fixtures with configured shipping and an offline/test payment flow; traverse address/method/payment/review/order/account behavior. Existing checkout-attribute tests do not prove checkout steps or order placement.
6. Observe independent deployment/upgrade using separate storage and keys, and finish the current release/browser evidence. Only then audit the full user goal for completion.
