# Deploy these two test storefronts

Use PowerShell 7 and Python with Paramiko (`python -m pip install paramiko`). This updates the existing server at 10.11.11.223; it does not create a new PostgreSQL service or reinstall the store.

```powershell
& 'C:/Users/s_jamili/Desktop/nopcomerce/nopCommerce/scripts/DeployTestServer/Deploy-TestServer.ps1' -Theme Boutique
& 'C:/Users/s_jamili/Desktop/nopcomerce/nopCommerce/scripts/DeployTestServer/Deploy-TestServer.ps1' -Theme Madadrang
& 'C:/Users/s_jamili/Desktop/nopcomerce/nopCommerce/scripts/DeployTestServer/Deploy-TestServer.ps1' -Theme Both
```

Each selected theme is published for linux-x64 in an isolated source snapshot, uploaded, and built into a new Docker image on the server. The application alone is recreated. Its database, uploaded images, files, and data-protection keys persist. The other application and the shared PostgreSQL container are checked for unchanged IDs and start times. Existing sample products and test orders are not reseeded.

For an already verified Linux release, pass `-BoutiqueAppPath` or `-MadadrangAppPath` to skip publishing that theme. Do not supply a Windows runtime package.

The default private configuration is `C:/Users/s_jamili/Desktop/nopcomerce/dual-store-deployment-20261012/deploy.private.json`. It references the owner SSH handoff, pinned SSH host key, and private administrator credentials. Copy `config.example.json` to a private location if configuring a different workstation; use `-PrivateConfig` to choose it. Passwords belong only in the private administrator file, with the shape `{ "madadrang": { "email": "...", "password": "..." }, "boutique": { "email": "...", "password": "..." } }`. Do not add credential files to Git. On the server this file has mode 600, under a mode-700 directory.

Every deployment activates and unlocks the designated installed administrator, ensures the Registered role, and applies the native configured salted hash only if the password differs. Native HTTP login and an authorized `/Admin` response are required. Verification sends a normal browser user agent because nopCommerce assigns crawler requests a search-engine customer before processing authentication.

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

Initial deployed native application source: 274fe50cf3371072a27865042c7af5ed33adafa6, Linux Release with UseSharedCompilation=false. The selected Boutique CSS additionally includes commit ef23c281d435; its SHA-256 is c3c5daff8c9f92b569ca4434e24ce50b52c4b431840ec918866f7e69164b89fa. Image tags on repeated deployments identify the deploying checkout and release time; an explicitly supplied prebuilt AppPath retains that package's original application source. The first native installations used the designated private administrator credentials.
