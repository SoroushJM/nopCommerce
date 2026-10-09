# Low-memory test deployment

Build on a machine with enough RAM; the runtime budget is deliberately much smaller.
Run commands from the repository root with Docker Compose v2 and BuildKit enabled.

```powershell
Copy-Item .env.example .env
# Edit .env and replace both example passwords before deployment.
docker compose build
docker compose up -d --wait --wait-timeout 180
```

Open https://localhost:2021 to install over HTTPS, or http://localhost:2020 for HTTP.
The initial HTTPS certificate
is self-signed for localhost, so browsers require an explicit trust exception.
It is generated once into the `https` volume, never embedded in the image.
For a real domain, replace `/https/nopcommerce.pfx` in that volume with a trusted
certificate and set the matching `HTTPS_CERT_PASSWORD`, then recreate the web service.
Changing the password alone does not re-encrypt an existing certificate.

## First installation

The database initialization enables `citext` and `pgcrypto` before the application starts.
Set `ADMIN_EMAIL` in `.env` to prefill the administrator email (default
`admin@example.com`). The first request opens the nopCommerce installer with
PostgreSQL already configured from `POSTGRES_DB`, `POSTGRES_USER` and
`POSTGRES_PASSWORD`. The database fields are omitted, and credentials stay on
the server. Set and confirm the administrator password, then submit the form.
The email is editable before installation. Changing `ADMIN_EMAIL` after
installation does not modify an existing administrator account.

Compose supplies these bootstrap values under `InstallationConfig`, keeping the
runtime `DataConfig.ConnectionString` unset until the installer creates tables.
Submitting browser database fields cannot override the configured database.
PostgreSQL connection pools are limited to 10 connections.

For a manual deployment without `InstallationConfig.ServerName`, the original
database form remains available. Choose PostgreSQL and supply:

```text
Host=nopcommerce_database;Port=5432;Database=nopcommerce;Username=nopcommerce;Password=<POSTGRES_PASSWORD>;Maximum Pool Size=10;Minimum Pool Size=0
```

Use the database and user from `.env` if you changed them. The database already
exists; do not request database creation in a manual installation.
Sample data and downloading regional resources are disabled to reduce installation
work. No connection string is injected into an empty database: nopCommerce treats
a nonempty connection string as an already-installed database and skips installation.
The installer persists its configuration in `app_data`.
The protocol used for installation sets the store's initial SSL preference. Enable
SSL in the store settings and set its URL to `https://<host>:2021/` to serve HTTPS
after installation. Configure the matching host and port when deploying remotely.
Compose supplies the external port pair to `CommonConfig`; protocol redirects map
2020 to 2021 (and back) instead of preserving an incompatible port.

The first installed-store startup can take several minutes with the small-host CPU
limit, while plugins initialize and Razor views compile. If installation has
finished but the browser still shows its last progress message, restart only the
web service with `docker compose restart nopcommerce_web`, then reload the page.
Do not rerun installation or delete volumes to fix a stale progress screen.

## Runtime memory budget

| Service | Hard limit | Swap limit (RAM + swap) |
| --- | ---: | ---: |
| nopCommerce | 352 MiB | 352 MiB |
| PostgreSQL | 96 MiB | 96 MiB |
| Certificate helper, exits after generation | 16 MiB | 16 MiB |
| Total including helper | 464 MiB (486.54 MB) | 464 MiB |

These are cgroup limits, including container-accounted page cache. Once the helper
exits, the running services have a combined ceiling of 448 MiB (469.76 MB).
This budget excludes the host OS, Docker daemon, reverse proxies, and image builds.
For a shared 1 vCPU / 1 GB host, use the small-host overlay:

```sh
docker compose -f docker-compose.yml -f docker-compose.small-host.yml up -d --no-build
```

This overlay limits the web
service to 384 MiB and 0.35 CPU, and PostgreSQL to 64 MiB and 0.15 CPU.
The running RAM ceiling is 448 MiB; including the temporary certificate helper it
is 464 MiB (486.54 MB). Web swap is disabled to avoid prolonged memory reclaim;
up to 32 MiB database swap is permitted if the host has swap configured.
Use this profile only for light test traffic.
The shared test server installs a `docker-compose.override.yml` symlink to this
overlay, so normal `docker compose` commands keep these tighter limits.
It caps usage; it does not guarantee arbitrary traffic or large catalogs will fit.
An exhausted limit can cause an OOM kill. This profile is intended for light test use.

.NET uses workstation GC, a managed heap budget of 30% of its container limit
(about 106 MiB in the base profile and 115 MiB on the shared host), aggressive
memory conservation, and disabled diagnostic IPC.
The GC percentage environment variable is hexadecimal (`0x1E`).
Application cache entries default to five minutes and LINQ query caching is disabled.
WebOptimizer stores generated bundles on disk instead of keeping them in memory.
PostgreSQL uses 16 MB shared buffers, 1 MB work memory, 16 MB maintenance memory,
20 connections, one autovacuum worker, and no parallel workers or JIT.
Keep application pools at 10 connections as shown above.

```powershell
docker compose ps
docker stats --no-stream
docker compose logs --tail 100 nopcommerce_web
docker compose down
```

`down` preserves the database, configuration, images, uploaded files, icons and certificate.
Avoid `down -v` unless you intentionally want to erase the installation.
Plugins are shipped in the image, so rebuilding updates their binaries; plugin
uploads inside a running container are not persistent in this test profile.
PostgreSQL is reachable only inside the Compose network. PostgreSQL 18 uses the
volume mount `/var/lib/postgresql`; an older major-version volume cannot be reused
without a proper database migration.

## Build size and cache

The final image contains Alpine ASP.NET runtime and published application output.
SDK, Node, npm, NuGet packages, source code, test projects and preview app stay out.
Publishing targets `linux-musl-x64` and remains framework-dependent, so unrelated
platform-native assets and a duplicate .NET runtime are not shipped. For ARM64,
build on an ARM64 builder with `--build-arg RUNTIME_IDENTIFIER=linux-musl-arm64`.
Globalization and image-processing native libraries remain available.
Shared plugin assemblies are cleaned with nopCommerce's existing build utility
before publishing.
Byte-identical copies of native libraries and Razor reference assemblies already
supplied by the app are removed from plugin output by `compact-publish.sh`.
Dynamic plugins and Razor runtime compilation require assemblies, so trimming and
Native AOT are deliberately not enabled.

`src/Docker.slnx` contains the core store, Persian Storefront, CheckMoneyOrder,
FixedByWeightByTotal, FixedOrByCountryStateZip and Swiper. Other integrations are
excluded from this small test image. To add a plugin, add its project to that solution
and a matching metadata `COPY` above the restore step in `Dockerfile`.

NuGet restore copies only project metadata before source, and uses a persistent
BuildKit NuGet cache. npm installation depends only on its manifests and uses its
own download cache. Ordinary C# or Razor changes reuse dependency layers.
Use `docker compose build` again to reuse cache; do not use `--no-cache` routinely.

For deployment elsewhere, push `nopcommerce:low-memory` and
`nopcommerce:certificate-helper` to your registry, update their image names in
Compose, copy Compose, `.env` and `docker/postgres-init.sql`, then run
`docker compose up -d --no-build`.
`postgresql-docker-compose.yml` includes the same configuration for compatibility.

References: [Docker cache optimization](https://docs.docker.com/build/cache/optimize/),
[PostgreSQL official image](https://hub.docker.com/_/postgres).
