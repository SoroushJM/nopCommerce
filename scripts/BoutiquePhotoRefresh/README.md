# Refresh only the Boutique test photographs

`prepare-photo-import.py manifest.json output.sql` prepares a transaction for the existing `nop_boutique` deployment. The manifest must contain exactly BTQ-QA-001 through BTQ-QA-012, plus a hero photo. Each entry supplies `path` (or `localFile`), optional `mimeType`, `sourceUrl`, `licenseUrl`, `license` and `attribution`; product entries also supply `sku`. The hero `description` supplies its accessible image text; optional `storeDescription` explicitly updates the storefront hero copy.

The importer validates actual JPEG/PNG/WebP file signatures and a maximum 5 MB per file. It resolves remote product IDs by SKU, creates native Picture/PictureBinary rows, replaces only the existing single owned sample-picture mapping, and updates only the selected native hero setting. Existing Picture rows and binaries remain available for rollback. Applying the same photo bytes again reuses the current native picture. Product fields and attribute combinations are compared inside the transaction to prove that photo replacement does not change prices or stock.

Owned native attribute-value square images and value/combination picture tables are checked first. The reviewed deployment has zero such references. A future nonzero reference stops the transaction rather than leaving a stale option image. Generated metadata records every source, author, license, file digest and byte count.

`prepare-photo-credits.py manifest.json output.sql` prepares the public native topic `BoutiqueTestPhotoCredits` at `/boutique-photo-credits`, titled «منابع تصاویر», and links it from the owned help footer. Each unique source displays its source URL, author and license link; the page states that images were resized for the web. The reviewed Commons manifest contains 13 entries and 11 unique sources because the hero and two shirts intentionally share a clothing-rail photograph. Back up owned Topic, UrlRecord and footer MenuItem rows before applying.

Before applying, back up the owned Picture/PictureBinary/mapping records, hero settings, product fields and combinations. The initial remote backup is `/home/web/nop-dual-store-20261012/backups/boutique-photos-20261013/native-picture-backup.json` (mode 600). Clear only Boutique's native cache through authenticated `/Admin/Common/ClearCache`, or recreate only Boutique with the existing deploy workflow. Do not reseed the catalog, restart PostgreSQL or modify Madadrang.

`ensure-native-blog-menu.sql` repairs discoverability of the enabled native blog in the owned main/help menus. It adds a Persian-titled native Blog route item if missing and safely reuses its own existing item on later runs. It preserves the blog enable setting and unrelated menus. The existing test installation has no blog posts; the menu repair does not invent posts.

`ensure-native-madadrang-blog-menu.sql` performs the corresponding bounded repair in Madadrang's separately owned menus. `ensure-native-price-range.sql` changes only untouched native manual 0–10000 search/tag defaults to automatic price derivation on the Boutique test catalog. Filters remain enabled. Back up settings first, then clear that store's native cache through its public host.

The deployment helper applies `DeployTestServer/ensure-relative-media.sql` only to the selected guarded test database. Relative native image paths prevent internal readiness addresses from leaking into cached storefront thumbnails. Native default icons are restored into the selected persistent icon volume only when files are missing; existing uploaded icons are preserved.

Store all generated SQL and binary metadata outside the Git checkout. Public photo provenance belongs with the reviewed manifest; no credentials belong here.
