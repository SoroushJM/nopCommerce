# Storefront component contracts

Run from the repository root:

```text
dotnet test src/Tests/Storefront.Theme.Tests/Storefront.Theme.Tests.csproj
node --test src/Tests/Storefront.Theme.Tests/native-cart.test.mjs
```

The .NET tests render the actual Shared Razor components with HtmlRenderer. They check native account/navigation routes when OTP is disabled; serialized product attributes, quantity and cart-edit ID used by native forms; hidden/call-for-price suppression across alternative price displays; cart rental/recurring/discount/edit details and image/removal/allowed-quantity capability flags.

The dependency-free JavaScript tests load the shipped Shared module and the actual native `public.ajaxcart.js` in a VM. They check cart event cleanup, native cart request headers, repeated checkout values, item removal/quantity names, antiforgery refresh and login redirects. Bridge checks preserve native return values and popup/bar notification settings, synchronize count/wishlist/flyout HTML, and ensure Shared reads and mini cart updates cause no additional requests, events or notifications.

The server supplies `NativeCart.NativeSections` with the native response keys `updatetopcartsectionhtml` and `updateflyoutcartsectionhtml`. `readNativeCart` applies those sections silently through the original native handler; a native AJAX add dispatches one Shared refresh. Shared `postForm` applies add-response sections/notifications without dispatching another event because `NativeProductPage` already owns its cart event.

These tests do not require a database. They do not verify MVC fallback filters, theme selection, native header/footer/menu widgets, or actual browser focus and clicks. Those need the integrated host and browser checks.
