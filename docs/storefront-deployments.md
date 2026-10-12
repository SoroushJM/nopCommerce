# Independent storefront packages

The technical theme IDs are `Madadrang` (visible brand: مداد رنگی) and `Boutique`. Both use the shared nopCommerce bridge and the same native commerce services. A release contains one brand library, one set of theme assets/views, `DefaultClean` for native fallback pages, and the shared components. Keeping the native fallback views preserves account, checkout, orders, and configured nopCommerce features.

## Build selection

`StorefrontTheme` is a global MSBuild property:

| Value | Use |
|---|---|
| `Madadrang` | Default; compile and package مداد رنگی only |
| `Boutique` | Compile and package Boutique only |
| `All` | Development compilation of both themes; not a release package |

Invalid values fail the build. For selected-theme production builds use `src/Docker.slnx` or the publish script. The full `src/NopCommerce.sln` explicitly includes both UI libraries for development, so a full solution build is not the mechanism for producing an isolated brand release.

`Storefront.Client` conditionally references the selected brand assembly plus `Storefront.UI.Shared`. The Persian storefront plugin does the same, publishes the matching browser application, and copies assets into its `Content/Storefront.UI.<theme>` and `Content/Storefront.UI.Shared` folders. For `Boutique`, the four Madadrang-only plugin views are excluded from Razor compilation and copying. `Nop.Docker.Views` precompiles all native/default views and only the selected theme/plugin views.

`StorefrontPluginOutputRoot` optionally redirects the Persian plugin output into `<root>/Misc.PersianStorefront/`. Other native plugins retain their normal nopCommerce output convention. The publish script therefore builds in a fresh source snapshot to keep every plugin away from a running checkout's `Nop.Web/Plugins` folder.

`Nop.Docker.Views` exports its resolved plugin model DLL dependencies into `PrecompiledViewPluginDependencies` beside its build output. Both the publish script and Docker packaging put those Release assemblies beside `Nop.Docker.Views.dll` in the application root. This is required because MVC inspects compiled view types during startup, even when a referenced native plugin is available in `Plugins` but is not installed or loaded by nopCommerce yet. Plugin folders and installation metadata remain intact; publishing does not install those plugins. The package manifest lists the exact exported dependencies.

## Prepare a release

From the repository root, with .NET 10 SDK, Node/npm, Git, and PowerShell installed:

```powershell
./scripts/publish-storefront.ps1 -Theme Madadrang -Environment test -RuntimeIdentifier win-x64 -OutputDirectory C:/storefront-packages
./scripts/publish-storefront.ps1 -Theme Boutique -Environment test -RuntimeIdentifier win-x64 -OutputDirectory C:/storefront-packages
./scripts/publish-storefront.ps1 -Theme Madadrang -Environment production -RuntimeIdentifier linux-x64 -OutputDirectory C:/storefront-packages
./scripts/publish-storefront.ps1 -Theme Boutique -Environment production -RuntimeIdentifier linux-x64 -OutputDirectory C:/storefront-packages
```

Use `linux-musl-x64` for Alpine. Packages are framework-dependent and need the matching .NET 10 ASP.NET runtime. No package is deployed or started by this script.

Each run creates a new `<theme>-<environment>-<id>` directory with:

- `app/`: the complete host, native plugin outputs, precompiled views, selected browser application, and selected theme assets.
- `source/`: a snapshot of tracked and non-ignored source files used for that run; local build outputs, environment files, keys, and live plugin outputs are excluded.
- `package.json`: selected theme/environment/runtime and launch ports.
- `deployment.env.example`: matching Compose variables and placeholder credentials.

Existing package directories are not reused or deleted. Output must be outside the source checkout. A failed build retains its source/output for diagnosis. The script verifies the active CSS and browser framework are present and rejects inactive-brand binaries/assets in the release.

Runtime theme selection is explicit: `StorefrontThemeConfig__Theme=Madadrang` or `Boutique`. For a locally hosted package, set this environment variable before launching `dotnet Nop.Web.dll`. Also set `CommonConfig__UsePrecompiledViews=true` to use the packaged `Nop.Docker.Views.dll` without runtime Razor compilation. Configure ports/certificate/database per installation. Installation state and uploaded media are deployment data and are not copied from a developer checkout.

## Independent Docker instances

The image build argument is `STOREFRONT_THEME=Madadrang|Boutique`. `All` is rejected for the production Docker image. NuGet and npm caching is retained, while only the selected theme is built and shipped.

```powershell
docker build --build-arg STOREFRONT_THEME=Madadrang --target runtime -t nopcommerce:madadrang-test .
docker build --build-arg STOREFRONT_THEME=Boutique --target runtime -t nopcommerce:boutique-test .
```

For each installation, copy its generated `deployment.env.example` into a separate private environment file and set unique database/certificate passwords. Example instance mapping:

| Theme | Environment | Compose project | HTTP | HTTPS | Default database |
|---|---|---|---:|---:|---|
| Madadrang | test | `nopcommerce-madadrang-test` | 2020 | 2021 | `nopcommerce_madadrang_test` |
| Boutique | test | `nopcommerce-boutique-test` | 2030 | 2031 | `nopcommerce_boutique_test` |
| Madadrang | production | `nopcommerce-madadrang-production` | 3020 | 3021 | `nopcommerce_madadrang_production` |
| Boutique | production | `nopcommerce-boutique-production` | 3030 | 3031 | `nopcommerce_boutique_production` |

Override suggested ports with `-HttpPort` and `-HttpsPort` if they are already occupied. Compose requires explicit ports, a lowercase `STOREFRONT_THEME_SLUG` (`madadrang` or `boutique`), and `STOREFRONT_ENVIRONMENT`. Its project name, image tag, and named volumes include theme and environment. There are no fixed container names or shared writable data mounts. Separate projects have separate database containers/networks, app state, uploads, file exports, icons, data-protection keys, and TLS certificate volumes. Each instance's credentials/installation state belong to that instance.

Validate the rendered configuration first:

```powershell
docker compose --env-file C:/storefront-configs/madadrang-test.env -f docker-compose.yml config --quiet
docker compose --env-file C:/storefront-configs/boutique-test.env -f docker-compose.yml config --quiet
```

When deployment is intended, run the matching instance explicitly:

```powershell
docker compose --env-file C:/storefront-configs/madadrang-test.env -f docker-compose.yml up -d --build
docker compose --env-file C:/storefront-configs/boutique-test.env -f docker-compose.yml up -d --build
```

`postgresql-docker-compose.yml` is a compatibility entry point for the same isolated PostgreSQL deployment. `docker-compose.small-host.yml` is a resource overlay for a small PostgreSQL test instance; use the same per-instance environment file with both base and overlay files. It is not a shared database deployment.

`mysql-docker-compose.yml` offers an independent MySQL instance using the same theme/environment/port contract. Set `MYSQL_DATABASE`, `MYSQL_USER`, `MYSQL_PASSWORD`, and `MYSQL_ROOT_PASSWORD` instead of the PostgreSQL variables. Use a separate instance identifier/environment when running MySQL and PostgreSQL concurrently; their web ports and app volumes must remain different. The MySQL service has its own `mysql_data` volume and no fixed container name. Do not apply the PostgreSQL small-host database limits to MySQL.

## Development preview

The local `RendererProbe` lives in `Storefront.Preview`, not in a production UI library. It uses `InteractiveServer`; `/renderer/server` is its current route and `/renderer/auto` remains a compatibility alias. The preview refuses to run outside Development.

```powershell
dotnet run --project src/Storefront/Storefront.Preview -p:StorefrontTheme=Madadrang --environment Development
dotnet run --project src/Storefront/Storefront.Preview -p:StorefrontTheme=Boutique --environment Development
```

The preview selects the compiled theme CSS through assembly metadata, with an optional runtime `StorefrontTheme` configuration override. Preview output is not included in Docker release builds.

## Release verification

Verify both selected packages independently: the browser output must contain `Storefront.UI.Shared` and the active brand assembly only; the packaged `Themes` directory must contain `DefaultClean` and the active brand only. Confirm the active theme at runtime, then test native guest/customer cart, checkout, actual payment plugin callbacks, customer registration/login, order history, and native fallback pages. Build/package checks do not prove gateway behavior or every configurable plugin feature.
