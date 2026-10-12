# Storefront modal candidate acceptance — 2026-10-13

## Candidate identity

- Upstream source: BlazorBlueprint `primitives/v4.1.0`, commit `9534520512db530e640b7bca878d73f7c444874e`.
- Candidate package: `BlazorBlueprint.Primitives` `4.1.1-storefront.1`.
- SHA-256: `C31241A6C32419BDF673D4185911F47DC6429209F37868B0B0ED44B59D3CDD64`.
- License/provenance: Apache-2.0, with upstream LICENSE and NOTICE preserved in `vendor/blazorblueprint-primitives`.

## Test surface

An isolated Interactive Server harness used externally controlled Sheet and JavaScript-strategy Dialog state (`@bind-Open`) with the same portal host arrangement used by the storefronts. It ran separately from product and deployment sources.

## Browser acceptance

Both controls passed the following in an independent browser session:

| Case | Evidence |
| --- | --- |
| Normal close | At 60 ms, overlay and content remained mounted with `data-state="closed"` and `inert`; opacity was mid-exit. At about 280 ms, both roots were unmounted. |
| Focus and scroll | Close returned focus to the original opener by the 60 ms exit frame and after unmount. Body overflow was visible after close. |
| Escape | Escape followed the same retained-exit and cleanup lifecycle. |
| Rapid reopen | A Dialog reopened 50 ms into the 180 ms exit bridge with a non-inert open root, hidden body overflow, and focus on dialog content. Five Tab presses remained within the dialog. Its eventual close unmounted roots, unlocked the body, and restored the opener. |
| Reduced motion | At 30 ms after Sheet open there were no active animations and opacity was 1. At 30 ms after close its roots were unmounted, body scrolling was restored, and focus returned to the opener. |

Native HTML dialog behavior was intentionally not changed. The public Madadrang cart mounts the JavaScript Sheet strategy. Boutique's reviewed header/catalog paths use native MVC controls; its shared modal integration is covered by package integrity and this isolated harness, without claiming an actual public Boutique modal was exercised.

## Reproducible checks

Build and pack the candidate with:

```powershell
dotnet pack src/BlazorBlueprint.Primitives/BlazorBlueprint.Primitives.csproj `
  -p:MinVerVersionOverride=4.1.1-storefront.1 `
  -p:StorefrontExitPatch=true `
  -o candidate-artifacts
```

To run the small JavaScript coordinator contract, extract the package and pass its actual packaged module path:

```powershell
node vendor/blazorblueprint-primitives/tests/modal-exit.contract.mjs `
  <extracted-package>/staticwebassets/js/primitives/modal-exit.js
```

The contract covers missing portal completion, reduced-motion completion, cancellation on reopening, and limiting waits to explicit modal parts.

The checked-in rebuild script was executed from a fresh external directory on2026-10-13. Exact upstream checkout, patch application and Release packing all passed. The rebuilt coordinator also passed the durable Node contract. Coordinator and provenance text match the candidate after newline normalization; upstream LICENSE and NOTICE match byte for byte. Both assemblies retain version4.0.0.0. The rebuilt archive has a different hash because build paths/metadata and source newlines differ; the checked-in candidate remains immutable at the hash above.

Storefront integration pins the patched primitive version centrally, restores it from the checked-in local feed and copies native plugin assets from the resolved package path. Publication rejects releases missing the coordinator or primitive WASM. Both brand styles disable modal animations and transitions when reduced motion is requested. Live deployment acceptance remains required before closing DSN-008.
