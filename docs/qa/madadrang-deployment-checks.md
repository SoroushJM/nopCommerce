# Madadrang deployment checks — 2026-10-12

Target: the installed PostgreSQL Madadrang test store at `188.121.123.191:2020`,
Compose directory `/opt/nopcommerce`. The source database remained installed;
these checks did not reinstall nopCommerce. Application deployments used the
already loaded release image, including an alternate local tag of that image
to exercise the `--image` branch. A new application image was not built here.

## Database regression

`sh docker/test-madadrang-deployment.sh` completed every assertion in a disposable
database copy and removed the copy afterwards:

- Normal deployment against an empty scratch catalogue created zero products
  and zero categories.
- Subsequent explicit seed created eight products, four categories, 32 variants,
  eight picture mappings and eight nonempty picture binaries.
- Three owned menus, category menu entries and eight native product URLs existed.
- Public request culture was Persian and the Persian display currency was IRT.
- Repeating seed produced the same full database data checksum.
- Edited product price, stock, homepage visibility, menu title and translation
  survived both seeded and unseeded repeat migrations, with identical checksums.
- An unexpected Boutique theme caused an error before seed writes; the data
  checksum was unchanged.

The same insertion/idempotence checks also passed on a disposable local
PostgreSQL 18 database before server testing. The data-only checksum dump emits
a circular-constraint warning for DiscountRequirement; it is used only for
comparison. Baseline restoration uses a full custom-format dump.

## Actual deployments

| Command | Result | Backup directory relative to `/opt/nopcommerce` |
| --- | --- | --- |
| `sh docker/deploy-madadrang.sh --seed-data` | Exit 0; rendered verification passed | `backups/presentation-20261012T155142Z-518984` |
| `sh deploy.sh --image nopcommerce:madadrang-deploy-test` | Exit 0; rendered verification passed without seed | `backups/presentation-20261012T155502Z-521482` |

Both reported Persian/RTL HTML, no installer iPhone/Salaxy/welcome content,
four category tiles, eight product elements, twelve homepage assets, working
category routes, the catalogue route and a rendered product route. The seed
deployment also verified toman prices. The second deployment did not generate
or include seed SQL.

Shell syntax, LF line endings and Python compilation passed. The root wrapper's
help worked; invalid store IDs, missing image values and unknown flags were
rejected before deployment. Browser inspection confirmed Persian menus and the
Madadrang hero, with the old installer slider absent; product prices displayed
in toman. The existing application's homepage title remains `Your store` and
can be edited in store settings separately.

This covers deployment setup and its page verifier; payment, order submission
and every administrative workflow are outside these checks.
