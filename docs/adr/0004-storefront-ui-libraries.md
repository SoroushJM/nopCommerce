# Three storefront UI libraries and independent release selection

Date: 2026-10-12
Status: Accepted

## Context

The existing stationery UI combined generic interaction code with the visual identity of مداد رنگی. A second clothing storefront needs its own Persian, RTL design while retaining nopCommerce's native capabilities and the independence of the two installations agreed in ADR 0003. The user approved three UI projects in this repository and parallel implementation in isolated worktrees.

## Decision

- `Storefront.UI.Shared` owns generic UI contracts, small components and JavaScript interaction. It references neither brand project nor nopCommerce's server services.
- `Storefront.UI.Madadrang` owns the stationery brand components, CSS, fonts and images. Its visible brand is مداد رنگی; the technical identifier remains Madadrang.
- `Storefront.UI.Boutique` owns the clothing design and assets. Its visible brand comes from native store settings; Boutique is a temporary internal identifier.
- Each brand library references Shared. Neither brand references the other. Native MVC themes compose those libraries with existing nopCommerce views and controllers. Server adapters and settings live in `Misc.PersianStorefront`.
- `StorefrontTheme=Madadrang|Boutique` selects project references, theme files and compiled views for a release. `All` is a development-only convenience. Runtime `StorefrontThemeConfig__Theme` selects the installed theme without changing shared database state.
- The publish script builds in a fresh source snapshot outside the live checkout. Every package carries its own application, plugin assemblies, browser app and selected brand assets; no runtime Shared service is introduced.
- Native forms, permission gates, validators, widget zones and unsupported-product fallback remain authoritative. Boutique uses the complete native product forms; Shared product rendering is used only where its explicit support predicate succeeds.

## Consequences

Generic behavior can be maintained once, while visual changes remain independently controlled. A Shared change may require rebuilding both packages, but deploying either package cannot restart the other instance. Separate database, storage, keys, credentials, ports and container identities remain required. Native UI compatibility and brand CSS must be checked together; successful library builds alone do not prove runtime or payment integration.

Real clothing imagery must be supplied through the store's picture management. Mock SMS remains limited to local development; release account routes use native authentication until a real provider is configured.
