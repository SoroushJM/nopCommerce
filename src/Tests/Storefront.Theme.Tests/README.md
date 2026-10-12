# Storefront component contracts

Run from the repository root:

```text
dotnet test src/Tests/Storefront.Theme.Tests/Storefront.Theme.Tests.csproj
node --test src/Tests/Storefront.Theme.Tests/native-cart.test.mjs
```

The .NET tests render the actual Shared Razor components with HtmlRenderer. They check native account/navigation routes when OTP is disabled; serialized product attributes, quantity and cart-edit ID used by native forms; hidden/call-for-price suppression across alternative price displays; cart rental/recurring/discount/edit details and image/removal/allowed-quantity capability flags.

The dependency-free JavaScript tests load the shipped Shared module. They check cart event cleanup, native cart request headers, repeated checkout values, item removal/quantity names, antiforgery refresh and login redirects.

These tests do not require a database. They do not verify MVC fallback filters, theme selection, native header/footer/menu widgets, or actual browser focus and clicks. Those need the integrated host and browser checks.
