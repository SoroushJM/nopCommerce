# Deploy these two test storefronts

For routine deployment, use the one-command image-transfer scripts from the repository root:

```powershell
pwsh -File ./scripts/deploy-storefront.ps1 -Theme Madadrang
pwsh -File ./scripts/deploy-storefront.ps1 -Theme MishaShop
```

The per-shop `deploy-madadrang.ps1` and `deploy-mishashop.ps1` wrappers now target independent self-host projects; see [that deployment guide](../../docs/smsir-selfhost.md). Do not point them at this existing dual-store layout.

This path requires PowerShell 7.4 or newer and uses SSH.NET in the existing .NET
runtime. It has no Python or Paramiko dependency. Named PowerShell functions keep
configuration, image building, transfer, startup, rollback and cleanup separate.

They build the selected image locally, transfer it over pinned SSH, replace only that
storefront, attempt rollback on startup failure, and retire its immediately previous
unused image after successful startup. They do not run HTTP checks, administrator
provisioning or SQL maintenance. See [setup, records and cleanup rules](../../docs/one-command-deployment.md).

## Older source-transfer and maintenance tools

The commands below are the older path, which builds images on the remote server
and also provisions administrator accounts. They remain documented for explicit
maintenance work; routine image deployments use the entry points above.

Use PowerShell 7 and Python with Paramiko (`python -m pip install paramiko`). This updates the existing server at 10.11.11.223; it does not create a new PostgreSQL service or reinstall the store.

```powershell
& 'C:/Users/s_jamili/Desktop/nopcomerce/nopCommerce/scripts/DeployTestServer/Deploy-TestServer.ps1' -Theme Boutique
& 'C:/Users/s_jamili/Desktop/nopcomerce/nopCommerce/scripts/DeployTestServer/Deploy-TestServer.ps1' -Theme Madadrang
& 'C:/Users/s_jamili/Desktop/nopcomerce/nopCommerce/scripts/DeployTestServer/Deploy-TestServer.ps1' -Theme Both
```

Each selected theme is published for linux-x64 in an isolated source snapshot, uploaded, and built into a new Docker image on the server. The application alone is recreated. Its database, uploaded images, files, and data-protection keys persist. The other application and the shared PostgreSQL container are checked for unchanged IDs and start times. Existing sample products and test orders are not reseeded.

Before uploading, the script checks free space for the compressed archive, three copies of the unpacked release (extracted context, BuildKit input and image layer), plus 256 MiB headroom. An insufficient-capacity result stops before uploading or changing data/services. The script performs no automatic pruning; verified task-owned release/cache maintenance must supply capacity before retrying.

For an already verified Linux release, pass `-BoutiqueAppPath` or `-MadadrangAppPath` to skip publishing that theme. Do not supply a Windows runtime package.

The default private configuration is `C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/deploy.private.json`. It references the owner SSH handoff, pinned SSH host key, and private administrator credentials. Copy `config.example.json` to a private location if configuring a different workstation; use `-PrivateConfig` to choose it. Passwords belong only in the private administrator file, with the shape `{ "madadrang": { "email": "...", "password": "..." }, "boutique": { "email": "...", "password": "..." } }`. Do not add credential files to Git. On the server this file has mode 600, under a mode-700 directory.

Every deployment idempotently provisions only the designated administrator, activates and unlocks it, ensures the Registered and Administrators roles, and applies the native configured salted hash only if the password differs. Existing accounts and store data are preserved. By default deployment checks native HTTP login and `/Admin`. When the owner will review the UI and requests no automated checks, pass `-SkipHttpVerification`: deployment uses database/account metadata and container startup logs, and sends no readiness or authentication requests.

For the two initialized local PostgreSQL stores, use the private settings in `%LOCALAPPDATA%/MadadrangDev/local-settings.json` (`AdminEmail` and `AdminPassword`) and run:

```powershell
./scripts/seed-storefront.ps1 -AdminOnly -Theme Madadrang
./scripts/seed-storefront.ps1 -AdminOnly -Theme Boutique -AppPath 'C:/Users/s_jamili/Desktop/nopcomerce/runtime-verification/local-mishashop-20261013/app'
```

This path runs before the installed-plugin shortcut, never makes HTTP requests, does not reinstall the plugin or reseed products, and guards the configured localhost database names. It uses the same native salted-hash helper as remote deployment. Keep credentials in the private settings file; never commit it.

To provision both existing remote test administrators without rebuilding applications or making HTTP requests:

```powershell
python ./scripts/DeployTestServer/provision-admin.py --config 'C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/deploy.private.json' --theme both
```

The helper uses the configured pinned SSH host key and private administrator file. It prints only designated account/role metadata and whether the configured native password was applied or already matched.

Readiness and authentication verification use the configured public storefront host. The guarded selected-store media setting uses relative native thumbnail paths, and verification rejects rendered private-network image URLs. This prevents a container-address health request from filling native picture caches with inaccessible URLs. Missing native `icons_0` assets are copied into the selected persistent icon volume without replacing existing files.

Server directory: `/home/web/nop-dual-store-20261012`. Compose project: `nop-dual-store-20261012`. Application services: `madadrang` and `boutique`; shared service: `database`. Databases: `nop_madadrang` owned by `madadrang`, and `nop_boutique` owned by `boutique`. PostgreSQL has no published port. The old `nginx-rdp-proxy` container is retained and stopped. GitLab is outside this project.

Madadrang: http://80.75.5.82:9011/ (internal http://10.11.11.223:9011/).
Boutique: http://80.75.5.82:9012/ (internal http://10.11.11.223:9012/).

Scoped server operations:

```sh
cd /home/web/nop-dual-store-20261012
docker compose -f compose.yml -f images.override.yml logs --tail=100 boutique
docker compose -f compose.yml -f images.override.yml restart boutique
docker compose -f compose.yml -f images.override.yml restart madadrang
docker compose ps
```

Selected-database dumps and App_Data backups are retained under `backups/<release>/`, and image contexts under `releases/<release>/`. Old images are retained for rollback. If a verification fails, the script exits with failure and leaves the exact release and backups available for diagnosis; it does not automatically alter other services. To roll back, change only the selected service image in `images.override.yml` to its previous image from the release `before.json`, then recreate only that service with `up -d --no-build --no-deps --force-recreate <service>`.

These are test stores running Staging, with sample catalogs, offline sample payment instructions, and disabled outbound message templates and scheduled tasks. No real SMS or payment gateway is configured.

The selected image override also sets `NOP_COOKIE_PREFIX` to `.Nop.madadrang` or `.Nop.boutique`. Browser cookies are scoped to hosts rather than ports, so distinct native cookie names are necessary on this shared test IP. Authentication, guest-customer identity, antiforgery, session, culture and other native cookies use this stable prefix. Normal installations without this environment variable keep `.Nop`. The first upgrade to distinct names creates a fresh browser session; registered customer/cart data remain in each database and are available after login.

After both applications have this source version and their distinct prefixes, `verify-cookie-isolation.py` verifies both designated native administrator logins in one cookie jar and revisits both `/Admin` pages after the second login. It verifies distinct native authentication, customer and antiforgery cookie names and only reads each cart. Upload this helper to the server directory and run `python3 verify-cookie-isolation.py`; credentials remain in the existing private file and the result is saved to `cookie-isolation-verification.json` with mode 600. The separate `Verify-GuestCookieIsolation.ps1` helper verifies guest antiforgery requests using a fresh shared guest jar: both carts must be empty before each store's own token is posted with an empty `updatecart` request. It adds or removes no products and never posts to an administrator cart.

Old extracted Docker build contexts may be retired only after a complete recovery archive and every-file hash manifest are verified. Original archives, recovery metadata, selected-state backups and current/previous accepted images remain available. Capacity maintenance removes only obsolete, unused images owned by this deployment and never prunes unrelated Docker objects or database volumes.

Initial deployed native application source: 274fe50cf3371072a27865042c7af5ed33adafa6, Linux Release with UseSharedCompilation=false. The selected Boutique CSS additionally includes commit ef23c281d435; its SHA-256 is c3c5daff8c9f92b569ca4434e24ce50b52c4b431840ec918866f7e69164b89fa. Image tags on repeated deployments identify the deploying checkout and release time; an explicitly supplied prebuilt AppPath retains that package's original application source. The first native installations used the designated private administrator credentials.
