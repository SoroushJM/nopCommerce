# Native stylesheet cascade and Head rendering

## Scope

Both themes retain DefaultClean styling for native forms, plugin views and advanced controls. Tailwind styles use named cascade layers. Loading DefaultClean as an unlayered stylesheet caused its universal padding/margin reset to override layered utilities, even when the theme bundle appeared later in the document.

`wwwroot/css/storefront-native-base.css` and its RTL variant import the original DefaultClean stylesheet into the `native` layer. The shared layer order places it after Tailwind's base and before component and utility styles. Both theme input stylesheets declare the same order. Importing the original file also preserves its relative image URLs.

The Head partials emit the wrapper as a direct versioned link before generated bundles. They remove only `NopLinkTagHelper` within those partials; the standard ASP.NET stylesheet version helper remains enabled. Native scripts and other registered styles continue through nopCommerce's existing pipeline.

## Regression found during isolated preview

Passing `Url.Content("~/css/...")` to nopCommerce's bundling link helper produced a resolved `/css/...` path. Its current `GetSrcUrl` implementation assumes a local input starts with `~/` and removes the first character. This converted the path to `css/...`, causing `PathString` to throw and Home to return HTTP 500. Removing the bundling helper from these two Head partials prevents that conversion and preserves the direct stylesheet link.

## Evidence and pending verification (2026-10-12)

- Both independent Release packages compile and publish after the Head correction with zero warnings and zero errors, as reported by the build agent.
- The wrapper stylesheet on each existing isolated preview responds HTTP 200 and contains the `layer(native)` import.
- The active preview processes still load the previous compiled Head. Automatic approval review rejected the process restart helper with `blocked by policy`, without a detailed reason. The rejected action was not retried through another tool or command.
- Home HTTP 200, the rendered direct link, computed spacing/color, and desktop/mobile appearance must be checked after the corrected package runs. Build success and a static stylesheet response do not establish those results.

Original development port 5090 and its database were not restarted or modified by this check.
