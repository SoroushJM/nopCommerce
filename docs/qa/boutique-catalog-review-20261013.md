# Boutique catalog review — 2026-10-13

## Scope

Public Boutique catalog verification at `http://80.75.5.82:9012/catalog/all` used the rendered native controls at a 390 px viewport. No cart, order, registration, or account action was used.

## Results after the category-zero repair

| Interaction | Observed result |
| --- | --- |
| Initial all-products catalog | Six cards on page 1, with twelve products across the catalog. The viewport had no horizontal overflow. |
| Sort by price, low to high | The generated URL retained `cid=0`; six cards remained on the current page. The prior accidental first-category narrowing did not recur. |
| Page 2 | Six cards rendered on page 2. The earlier zero-card result did not recur. |
| Boutique tops category route | Three cards rendered. |
| Return to all-products catalog | The unfiltered catalog returned to six cards on page 1, representing the twelve-product catalog. |
| Header search for `شومیز` | The native search route returned two matching product cards. |

## Advanced-search empty query

Submitting the native advanced-search form with an empty search term, including a category choice, renders the native validation warning that the search phrase must contain at least three characters. It does not present product results. This is validation behavior for the empty-query search route, not evidence of a catalog product defect.

