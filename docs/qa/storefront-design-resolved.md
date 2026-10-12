# Resolved storefront design findings

Updated: 2026-10-13. Open findings live in [the active checklist](storefront-design-open.md). Each entry records a completed deployed repair and its verification, rather than only a successful build.

## DSN-004 — Enabled Blog missing from active navigation

Resolved: 2026-10-13, both deployed themes.

The native blog was enabled but its link remained only in the unpublished default menu. The active owned main/help-footer menus omitted it. Added native published `Blog` route menu items titled `وبلاگ`, preserving all existing menu entries and the merchant's enable setting. Cleared each affected application's native cache; no app/PostgreSQL restart was required.

Fresh public Home HTML on both Madadrang9011 and Boutique9012 contains two `/blog` links: header/main and footer. Native `/blog` returns200. Deployment evidence: `C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/blog-public-dom.json`; the deployment agent may keep its timestamped task copy alongside this report. Browser review additionally observed both Boutique links on the cart page. The fresh databases contain zero posts; no invented posts were added.

## DSN-005 — Search sorting hides valid Boutique products through an incorrect price range

Resolved: 2026-10-13, deployed Boutique.

Searching `آوا` initially found the linen shirt, but selecting price ascending submitted the untouched native manual range0–10000 against a1,290,000-toman sample product. Changed only the default native search/tag manual-range flags to automatic derivation. Price filtering remains enabled; category/manufacturer ranges were already automatic.

Terra's actual browser retest generated an `orderby=10` URL without the erroneous price restriction and retained the matching product. Product prices, stock and commerce services were not changed. The guarded reusable settings repair is `scripts/BoutiquePhotoRefresh/ensure-native-price-range.sql`; native setting/menu backups remain in the selected server backup directory.

## DSN-001 — Boutique hero headline wrapping

Resolved: 2026-10-13, deployed Boutique. Balanced wrapping and responsive heading sizing prevent the isolated «تو» line. Terra's actual final live 390×844 review confirmed coherent text. Served CSS SHA256: `EE0328C416049C5498D048A82C3E40D58DFEE7799A4FF149118BFB1EF3554045`; source commit `d2514488b3`.

## DSN-002 — Boutique mobile header height

Resolved: 2026-10-13, deployed Boutique. Reduced spacing while retaining the native language, account, cart, search and menu controls, including a44px menu target. Terra measured the final live header at318px, hero start at y332, and body/client width390/390 at390×844. The earlier header was approximately418px. Final evidence: `runtime-verification/boutique-live-final-menu-open.png`; image review was performed by Terra medium only.

## DSN-003 — Real Boutique clothing photographs

Resolved: 2026-10-13, deployed Boutique. Imported licensed Wikimedia Commons garment/accessory photos for the twelve owned sample SKUs and hero through native Picture/PictureBinary services and mappings. Thirteen files represent eleven unique sources; the clothing-rail photo is intentionally reused. Terra visually checked the assets and final live product grid: photos loaded, fit contained cream frames, and include no human models. Public native topic `/boutique-photo-credits` and footer link provide authors, sources, licenses and resize disclosure.

Deployment proof compares all twelve Product rows and66 native combination rows before/after, unchanged, with no created orders. Existing pictures are retained for rollback; repeated import reuses all thirteen new picture IDs. Evidence: `C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/real-photo-preservation-proof.json`, `public-media-photo-verification.json`; visual evidence: `runtime-verification/boutique-live-final-products.png`. Reviewed public assets and manifest: `scripts/BoutiqueRealPhotos/`.

## DSN-006 — Docker-private native thumbnail URLs

Resolved: 2026-10-13, both deployed stores. Set the guarded native `MediaSettings.UseAbsoluteImagePath` to false and clear the affected native caches through their public origins. Future deployment readiness/authentication uses the public host and rejects private-network rendered image URLs. Native images continue to use their existing picture processing.

Root independently fetched both public Home pages:8 Madadrang and13 Boutique thumbnail URLs, all relative, zero private-network media URLs; representative images return200/image/jpeg. This closes the inaccessible-address fault, rather than claiming all browser image-loading timing is settled. Evidence: `C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/root-public-resource-verification-20261013.json`.

## DSN-009 — Missing native web manifest and app icons

Resolved: 2026-10-13, both deployed stores. Empty persistent icon volumes hid the packaged native defaults. Restored only missing `icons_0` assets; subsequent deployments skip existing icon files. Root independently parsed each public manifest and fetched all seven referenced PNG URLs: every manifest and referenced icon returns200 on both ports9011/9012. Evidence: `C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/root-public-resource-verification-20261013.json`.

## DSN-007 — Boutique native mobile menu transition

Resolved: 2026-10-13, deployed Boutique. Scoped the transition to the actual nopCommerce markup `.menu-container.menu-dropdown > .menu` and native active class `menu-dropdown--active`, retaining the native toggle and `aria-expanded`. Terra observed live requestAnimationFrame transition properties `max-height, opacity, visibility` with durations `0.24s, 0.18s, 0s`; the menu stays visible during closing and becomes hidden after240ms. In the open state all eight links are available, clientHeight/scrollHeight464/464; larger menus may scroll up to70vh. In the closed state programmatic focus on the first link fails. Reduced-motion emulation computes transition duration `0s`. Final served CSS is the same EE0328 release recorded above.

Madadrang's final fresh390px page has body/client width390/390. After a settled20-second browser pass the hero and first four JPEG thumbnails are complete; below-fold lazy images are pending, rather than decode failures. Root independently fetched all eight native JPEG thumbnails with200 responses. This supersedes the early three-second `naturalWidth=0` observation; no new defect is counted for pending lazy loading.
