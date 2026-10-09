# Native product shell QA

Date: 2026-10-09. Branch: `refactor/native-theme-client-ui`. Local host: `http://localhost:5090/product/productdetails?productId=1`.

This is partial phase 2 acceptance, not completion of homepage/catalog migration or final Persian visual acceptance.

## Implemented boundary

The native product Theme layout composes the shared header, BB menu, footer and mobile navigation. Main navigation uses the first eligible native main menu; the footer shows every eligible native footer menu. Menu titles, URLs, ordering, publication and authorization come from `IMenuModelFactory`. Runtime code does not reconstruct category links from a fixed name array.

Header/cart/account controls use one WASM island. Product controls retain their own component and notify the header after a successful native add through a small disposable browser-event listener. One page-wide `BbPortalHost` avoids duplicate Select/Sheet/Dialog portals. Cart reads/quantity and OTP still use the documented temporary adapter; the native add/price actions are already standard nopCommerce endpoints.

The homepage and legacy pages remain on their existing routes. Homepage/category data is not fetched by this header adapter; the native homepage will obtain its own prepared models in its section.

## Independent review and fixes

Fresh gpt-6.1-sol/xhigh review found duplicate portal hosting, missing native header-links widget model data and disabled BB dropdown keyboard navigation. The product portal host was removed, `HeaderLinksModel` is passed to header-links widgets, and `EnableKeyboardNavigation` was enabled.

Widget calls are retained, but selectors/middle/header-links/menu zones currently surround the header island. Their exact original relative placement is unfinished. This implementation does not claim full widget layout compatibility.

## Luna computer-use evidence

- Desktop 1440×900: document client width/scroll width both 1425, no horizontal overflow. Header/search/account/cart, main navigation and footer visible.
- Mobile 390×844: document client width/scroll width both 375, no horizontal overflow. Category disclosure opens/closes and the bottom navigation is visible.
- Orange selection shows unavailable and disables purchasing. Blue quantity two opens exactly one Sheet, displays 370,000 and badge two. Increasing to three displays 555,000 and badge three; removal returns badge zero.
- Mobile blue add likewise opens one Sheet and displays quantity one/185,000. Removal empties the cart.
- Account panel opens and closes. With it open, six DOM IDs were checked and none duplicated; after closing, no dialog remained.
- This browser session was already signed in. Luna did not sign out or submit OTP; the OTP flow is covered separately by TUnit.
- Browser console errors/warnings were empty. No WebSocket creation or handshake was observed after load.
- Desktop product, expanded mobile menu and mobile cart screenshots were captured inline through computer-use. No standalone PNG file is claimed.

The current native main menu has six plain English standard-page links and no children. Nested menu keyboard behavior was corrected by source review but could not be exercised with this fixture. Menu/footer localization, fixture configuration, category destination visual parity and exact widget placement remain open.

## Automated validation

Plugin/client build passed with zero warnings/errors. Tailwind was rebuilt using its CLI. Microsoft VS Code providers formatted the modified Razor/JS views and the follow-up formatter verification converged. C# whitespace formatting uses `dotnet format` with nopCommerce's existing `.editorconfig`.

The independent TUnit project passed all 15 scenarios, including native SSR/CSRF/product/cart, the temporary API/OTP contracts and administrator localization. This report does not treat those HTTP checks as a substitute for visual QA.
