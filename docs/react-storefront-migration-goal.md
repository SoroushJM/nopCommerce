# React storefront migration goal for review

Status: Draft for review. Do not begin implementation.

Date: 2026-10-13

I want to replace the customer-facing Blazor interface with React, backed by typed APIs that reuse nopCommerce's existing public controller logic. I want another agent to review the architecture and scope before I start the migration.

This request authorizes writing this proposal and reviewing it. It does not authorize implementing the migration, removing existing functionality, deploying changes, or starting an active automated goal.

## My intended outcome

Use React for the storefront and ASP.NET Core for the backend. Host the React application and its API on the same origin. Preserve nopCommerce's useful business logic, customer identity, cart behavior, and order processing while replacing the presentation contracts that currently depend on Razor views or Blazor components.

The migration is for customer-facing APIs only. I do not want an admin API or a React rewrite of the admin area.

## Decisions I have made

| Area | My intention |
| --- | --- |
| Frontend | Use React instead of Blazor for the customer-facing interface. |
| API starting point | Copy the relevant existing public controllers/actions and adapt them, preserving their business checks and service calls. |
| Responses | Convert view models into dedicated DTOs and return structured data instead of rendered views. |
| Return types | Use typed HTTP results, including `Task<Results<Ok<ProductDto>, NotFound>>`. |
| JSON | Use default System.Text.Json serialization for the new API responses. Preserving the old Newtonsoft response format is not a requirement. |
| Hosting | Serve React and the API on the same origin, meaning the same scheme, hostname, and port. |
| Authentication | Reuse nopCommerce's existing cookie authentication. React should not implement a separate authentication system or manage bearer tokens. |
| Scope | Customer-facing functionality only. Admin APIs are excluded. |
| Store | This is an Iranian store with a Persian interface and a need for an appropriate Iranian payment gateway. |
| Work now | Record and review the goal. Do not implement it yet. |

For different endpoints, use the result union that matches their actual outcomes. The product example does not mean every endpoint is limited to `200` and `404`. Checkout may need validation errors, conflicts, or other typed results. Middleware and filters may also reject a request before its action executes.

## What copying controllers means

My preferred starting point is the existing implementation, rather than independently recreating nopCommerce's backend behavior.

Preserve the applicable product availability checks, access control, store mapping, customer restrictions, cart validation, price and tax calculations, discounts, inventory behavior, events, and order processing. Keep those operations on the backend.

Adapt presentation-dependent branches and helpers as necessary. This includes `View`, `PartialView`, rendered HTML inside JSON, page redirects, `ModelState` errors, form inputs, and helper methods that return MVC result types.

Give the copied API controllers explicit routes that do not conflict with existing MVC routes. Review inherited and global filters: some return page redirects, and some custom filters check the original controller's exact type or require a `ViewResult`.

The reviewer should assess the maintenance cost of copied actions and recommend where shared helpers or existing services prevent behavior from drifting. Do not reinterpret this as permission to redesign the entire backend.

## Customer-facing scope to review

Inventory the public functionality before deciding the endpoint list. The goal is not automatically limited to the current development storefront's small set of JSON responses.

| Area | Functionality to account for |
| --- | --- |
| Catalog | Categories, search, filtering, sorting, pagination, product details, attributes, combinations, prices, and availability. |
| Cart | Guest and registered carts, add/update/remove, cart item editing, discounts, totals, and guest-cart migration on login. |
| Authentication | Existing customer login/logout flows, current-customer information, and session-expiration behavior. |
| Account | Profile, addresses, order history, and order details. |
| Checkout | Customer eligibility, addresses, shipping/pickup as applicable, totals, confirmation, order creation, and the selected payment integration. |
| Other public features | Inventory wishlist, reviews, returns, downloads, subscriptions, content pages, and plugin-backed public features. Identify which are required and which may be excluded. |

Installation, keep-alive, scheduled-task endpoints, and other infrastructure do not automatically need React equivalents. Exclusions from customer-facing capabilities must be explicit; do not silently remove features because they depend on the existing views.

Supporting all possible third-party plugin UIs is not an assumed requirement. Determine compatibility for the features and plugins the store actually uses.

## DTOs and JSON

New DTOs should describe the data React needs. They should not simply duplicate every property of a Razor view model. In particular, do not carry .NET component types, rendering helpers, or view-selection information into the API contract.

Default web JSON behavior is acceptable for the new responses. Avoid introducing dependencies on Newtonsoft-specific attributes or converters in new response DTOs. The existing MVC application can continue using its current Newtonsoft configuration while the new typed responses use System.Text.Json.

One separate decision remains: `TypedResults` controls output, but controller parameters using `[FromBody]` still use the host's MVC input formatter, which is currently configured with Newtonsoft. The reviewer should explain how to use System.Text.Json for incoming JSON as well if required, or whether keeping the existing input formatter for plain request DTOs is sufficient. Changing the return type alone does not switch request deserialization.

Generating TypeScript types and a client from OpenAPI was proposed as a way to keep React aligned with the DTOs. Review that option; no generator has been selected.

## Existing authentication and customer identity

The backend should validate credentials and issue the existing authentication cookie. The browser should store and send the cookie; React should not read it, store it in local storage, or implement its own identity validation.

React will still submit login/logout requests, request current-customer information, and handle authentication failures in the interface. Browser requests to the same origin include cookies by default. This assumption applies to browser requests; server-side React rendering, if chosen, needs its own explicit handling of request cookies.

Preserve nopCommerce's higher-level sign-in behavior, including guest-cart migration, current-customer updates, and login events. Do not reduce login to issuing a cookie if that bypasses those operations. Preserve guest-customer identification for shopping before login.

Cookie-authenticated state-changing API requests need backend antiforgery validation and a usable way for React to obtain and send the token. Review existing `[IgnoreAntiforgeryToken]` branches rather than copying them without assessment.

Use API-appropriate authentication failures. The project targets .NET 10, whose cookie handler recognizes API endpoints and normally returns `401`/`403`; explicit redirects in nopCommerce filters are a separate behavior to review.

The current custom OTP API is development-only. Production authentication and any real SMS sender must be assessed separately; the development API does not establish that production login is ready.

## Checkout and Iranian payments

I do not want to recreate every payment plugin's Razor interface in React. I am open to dropping generic payment-plugin UI support and supporting only the payment method or methods this Iranian store needs.

No specific gateway provider has been selected in this discussion. Identify the actual merchant provider/API before implementing payment. Referring to Shaparak alone does not specify the integration contract.

The proposed approach for review is to retain nopCommerce's payment extension point and implement or adapt one Iranian gateway integration through `IPaymentMethod`. Keeping that backend extension point does not require exposing arbitrary plugin UI to React. This is a proposed architecture, not a finalized provider choice or an instruction to remove payment infrastructure.

For a hosted redirect gateway, React should send the customer to the provider's payment page. Backend responsibilities include creating the payment attempt, storing its reference, verifying the provider response and amount, handling repeated callbacks/retries consistently, and updating the order through nopCommerce's order services. Make the currency and rial/toman conversion explicit.

The frontend's return page must not decide that an order is paid merely from callback query parameters. Payment verification belongs on the backend.

### The payment information step

`OpcSavePaymentInfo` is existing nopCommerce MVC checkout code. It loads the selected payment plugin, validates its `IFormCollection`, creates/saves a `ProcessPaymentRequest`, and renders the next checkout view into HTML inside a JSON response. It is not a custom React API written during this discussion.

`CheckoutPaymentInfoModel.PaymentViewComponent` is an existing `System.Type` property used by Razor to render plugin UI. It should not be copied into a response DTO.

For a hosted gateway that does not need extra payment fields on the store, the reviewer should assess skipping this separate screen using the payment method's `SkipPaymentInfo` behavior. The backend still needs to prepare the internal payment request and preserve order-processing requirements. A React-facing equivalent of `OpcSavePaymentInfo` may therefore be unnecessary.

If the chosen provider needs additional inputs, decide whether React submits form fields compatible with the existing plugin contract or a JSON request DTO that a backend adapter maps to those fields.

Checkout responses should contain data and navigation instructions, such as confirmation data or a payment redirect URL, rather than `update_section.html`. Include data currently obtained from nested view components, such as order totals, where the React screen requires it.

## UI constraints

Preserve the Persian, right-to-left storefront requirements and use the existing interface as the design reference unless I request a redesign. Determine which existing storefront themes must be migrated.

Follow the workspace styling rules: authored presentation uses Tailwind utilities, and absolute UI lengths use `rem`, including borders, shadows, radii, and breakpoint limits. Do not assume a Tailwind utility uses `rem` without checking. Do not perform styling migrations with bulk conversion scripts.

The React component library and rendering framework are undecided. Next.js or another SSR-capable React framework was suggested for product/category HTML and metadata, but I have not selected one. Review the rendering and deployment options. Existing Razor/BlazorBlueprint architecture documents describe the current implementation and should not be treated as approval to reject the requested React direction.

## Current source context

The current storefront combines Razor MVC and Blazor components. Replacing only the Blazor components is different from exposing customer-facing APIs for an independent React storefront; the latter is the direction I want reviewed.

The source inventory recorded during this discussion was:

| Area | Concrete controller classes | Abstract bases |
| --- | ---: | ---: |
| Public core | 22 | 1 |
| Admin | 62 | 1 |
| Plugins | 58 | 0 |
| Web framework | 0 | 3 |

These counts are for the main source tree, excluding release copies and build outputs. They do not describe enabled runtime plugins or define the final API endpoint count. Admin is outside this migration.

The existing `/stationery/api` exposes development-only OTP operations. Custom product/cart result filters provide limited JSON responses; they are not a complete public API. The `WebApi.Frontend` source in this checkout contains configuration UI and a link to obtain the official plugin, rather than a complete storefront API implementation.

The payment projects found in this checkout are PayPal Commerce and Check/Money Order. An external Zarinpal plugin exists, but its marketplace listing states support for nopCommerce 4.60/4.70, while this checkout declares 5.00. Compatibility and the gateway API need review before reuse.

Useful source and project references:

- [Public controllers](../src/Presentation/Nop.Web/Controllers/)
- [Checkout controller](../src/Presentation/Nop.Web/Controllers/CheckoutController.cs)
- [Checkout payment model](../src/Presentation/Nop.Web/Models/Checkout/CheckoutPaymentInfoModel.cs)
- [Payment interface](../src/Libraries/Nop.Services/Payments/IPaymentMethod.cs)
- [Payment service](../src/Libraries/Nop.Services/Payments/PaymentService.cs)
- [Order processing](../src/Libraries/Nop.Services/Orders/OrderProcessingService.cs)
- [Cookie authentication](../src/Libraries/Nop.Services/Authentication/CookieAuthenticationService.cs)
- [Customer sign-in and cart migration](../src/Libraries/Nop.Services/Customers/CustomerRegistrationService.cs)
- [MVC and authentication configuration](../src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/ServiceCollectionExtensions.cs)
- [Current custom OTP controller](../src/Plugins/Nop.Plugin.Misc.PersianStorefront/Controllers/PersianStorefrontController.cs)
- [Current storefront feature coverage](storefront-feature-coverage.md)
- [Existing architecture decision](adr/0002-native-theme-client-interactivity.md)
- [Microsoft controller result documentation](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0)
- [nopCommerce payment integration documentation](https://docs.nopcommerce.com/en/developer/plugins/payment-method.html)
- [Zarinpal plugin listing](https://www.nopcommerce.com/en/zarinpal-payment-noptop)

## What I want from the reviewing agent

Review this proposal and the source without implementing the migration. Give me a concrete assessment that answers:

1. Which public controllers/actions can be copied and converted directly, and which need presentation or workflow changes?
2. What public endpoint and DTO inventory would cover the required storefront features, and which feature decisions remain open?
3. How should the typed controllers be routed and hosted while preserving existing business checks and avoiding MVC route/filter conflicts?
4. How should request JSON parsing, response JSON serialization, validation errors, and OpenAPI be configured?
5. Can existing cookie authentication, guest identity, and cart migration be reused as intended? What API adjustments are needed?
6. What is the smallest correct checkout flow for the selected Iranian gateway, and can its payment-information screen be skipped?
7. Should a compatible existing gateway plugin be adapted, or should a new `IPaymentMethod` integration be written? Identify the actual provider information needed.
8. What React rendering approach best fits the storefront, metadata, existing visual requirements, and same-origin hosting?
9. What phases, acceptance criteria, and approximate effort would make the migration reviewable? State assumptions behind estimates.

Distinguish my confirmed choices from proposed implementation details. Identify concrete source dependencies rather than assuming all MVC behavior survives a change of return type. Do not start coding, delete existing UI, modify accepted architecture decisions, deploy, or start a running goal until I explicitly ask for implementation.
