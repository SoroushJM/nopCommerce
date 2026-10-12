# Madadrang cart UI review — 2026-10-13

## Scope

Public Madadrang verification at `http://80.75.5.82:9011/` used a newly cleared, agent-owned browser-cookie session and the rendered native storefront controls. No order, registration, SMS, or checkout action was performed.

## Preconditions

- The mobile header cart badge showed **0**.
- A single native catalog interaction added Product 1.
- The cart badge changed to **1**, and the public cart Sheet displayed one line.

The private session cookie and native cart-line identifier were preserved in a non-repository runtime artifact for recovery auditing. They are intentionally excluded from this document.

## Results

| Native UI action | Observed count before → after | Observed result |
| --- | ---: | --- |
| Add one Product 1 line | 0 → 1 | Cart Sheet opened with the new line. |
| Increase quantity | 1 → 2 | Request completed with HTTP 200. |
| Decrease quantity | 2 → 1 | Request completed with HTTP 200. |
| Remove line | 1 → 0 | Request completed with HTTP 200; no cart line remained. |

The test finished with the owned cart empty. The session was not reused for an order or account flow.

## Earlier scripted discrepancy

A separate nonempty-cart HTTP reproduction had reported a 404 for an update request while an empty-cart update returned 200. This browser-driven test did **not** reproduce that result: rendered native controls completed add, increase, decrease, and remove successfully. The evidence establishes that the public native UI path works for the owned guest session; it does not establish the cause of the difference from the separate scripted request.
