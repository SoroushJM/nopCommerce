# Open storefront design findings

Updated: 2026-10-13. Visual review is performed by Terra medium; root/deployment use text and HTTP diagnostics. Use the user-approved brief from `shared-chat-full.json` and the installed Frontend Design skill. Do not change the existing Madadrang identity to address Boutique findings.

This file contains only open findings. After a repair passes on the deployed website, remove its entire entry here and append the same ID, repair and evidence to [the resolved findings](storefront-design-resolved.md). Source edits and injected-CSS previews alone do not close an item. False observations are corrected in the review audit; they are not counted as repaired defects.

- [ ] **DSN-008 — Shared sheet closes before its exit animation can run.** Terra's frame-level review confirmed the Madadrang sheet has two real entry animations, but Escape unmounts content before the next frame; no closed-state element remains for exit motion. Idle `getAnimations()===0` is not a defect and is excluded from the findings. Pending: verify the installed Blueprint lifecycle/API and implement a compatible shared repair if needed, retaining focus restoration, modal locking and reduced motion. Owner: functional reviewer.

- [ ] **DSN-010 — Boutique all-products page cannot resolve native catalog partials.** Actual public navigation to `/catalog/all` returns the internal-error page; deployed logs name missing `_CatalogSelectors` under the custom MadadrangCatalog controller. Repair must preserve themed and native nested partial lookup, then pass actual catalog/filter/sort navigation after a compiled release.
