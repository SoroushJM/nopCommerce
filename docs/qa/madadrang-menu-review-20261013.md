# Madadrang public-menu review — 2026-10-13

Target: `http://80.75.5.82:9011/` (the deployed Madadrang storefront). This review used a fresh anonymous browser profile. No cart item, order, account, or other persistent shopper data was created.

## Actual browser interactions

| Surface | Interaction | Observed result | Result |
| --- | --- | --- | --- |
| Desktop header/main navigation | Clicked the catalog entry (`/stationery/catalog`) and allowed the redirect/navigation to settle | `302` to `/catalog/all`; rendered native catalog title **مداد رنگی. فروشگاه نوشت‌افزار** and H1 **دنیای رنگی نوشت‌افزار** | Pass |
| Desktop native category entry | Clicked **دفتر و کاغذ** (`/madadrang-category-1`) | Rendered title **مداد رنگی. دفتر و کاغذ** and H1 **دفتر و کاغذ** | Pass |
| Desktop footer | Clicked **تماس با ما** (`/contactus`) | Rendered title/H1 **تماس با ما** | Pass |
| Mobile, 500 CSS px viewport (Chrome headless minimum for the requested 390px window) | Opened the categories/hamburger sheet | Sheet exposed all four category links, catalog, offers, and Blog; its visible `role=dialog` state was `open` | Pass |
| Mobile category sheet | Clicked **نوشت‌افزار** (`/madadrang-category-2`) | Rendered title **مداد رنگی. نوشت‌افزار** and H1 **نوشت‌افزار** | Pass |
| Mobile category sheet | Dispatched Escape after opening the sheet | The dialog was removed/closed | Pass |
| Mobile bottom navigation | Opened the cart button | Empty cart drawer opened and showed count `0` | Pass |
| Mobile bottom navigation | Clicked the account entry (`/login`) | Network recorded `GET /login` with `200`; native login form rendered | Pass |

The short-lived observation that the catalog/category links returned to home was superseded by the redirect and final-DOM traces above. It was an early navigation-timing artifact, not a menu defect.

## Published destinations: HTTP GET chains

Each row uses a public **GET** request with redirects followed. Titles and H1 values are taken from the final returned page. `HEAD` was deliberately not used because this storefront handles some native catalog routes differently for HEAD than for GET.

| Published link | HTTP chain | Final title | Final H1 / expected content | Result |
| --- | --- | --- | --- | --- |
| `/stationery/catalog` (desktop/mobile Products) | `302 /catalog/all` → `200` | مداد رنگی. فروشگاه نوشت‌افزار | دنیای رنگی نوشت‌افزار | Pass |
| `/catalog/all` (All products) | `200` | مداد رنگی. فروشگاه نوشت‌افزار | دنیای رنگی نوشت‌افزار | Pass |
| `/madadrang-category-1` | `200` | مداد رنگی. دفتر و کاغذ | دفتر و کاغذ | Pass |
| `/madadrang-category-2` | `200` | مداد رنگی. نوشت‌افزار | نوشت‌افزار | Pass |
| `/madadrang-category-3` | `200` | مداد رنگی. لوازم مدرسه | لوازم مدرسه | Pass |
| `/madadrang-category-4` | `200` | مداد رنگی. هنر و خلاقیت | هنر و خلاقیت | Pass |
| `/catalog/all?orderby=10` (offers) | `200` | مداد رنگی. فروشگاه نوشت‌افزار | دنیای رنگی نوشت‌افزار | Pass |
| `/cart` | `200` | مداد رنگی. سبد خرید | سبد خرید تو | Pass |
| `/customer/info` (footer Account) | `302 /login?returnUrl=%2Fcustomer%2Finfo` → `200` | مداد رنگی. ورود | خوش آمدید؛ وارد حساب خود شوید. | Pass, expected guest authentication redirect |
| `/contactus` | `200` | مداد رنگی. تماس با ما | تماس با ما | Pass |
| `/blog` | `200` | مداد رنگی. وبلاگ | وبلاگ | Pass |

## Scope and limits

- This review covers anonymous public navigation and menu discoverability only. It deliberately did not repeat checkout or add a cart line.
- The Blog link was present in the mobile category sheet after the native menu update. Its route and native page were verified by HTTP; post-update desktop/footer placement was not separately clicked in this bounded pass.
- A follow-up Chrome DevTools `Emulation.setDeviceMetricsOverride` request explicitly set `390×844`, mobile mode, and a device scale factor of `1`, then reloaded the page. This headless Chrome session still exposed `innerWidth=500` and `screen.width=800`, so it could not establish an actual 390-CSS-pixel viewport. At the resulting mobile breakpoint, the category sheet opened, exposed four category links, closed on Escape, the bottom navigation remained visible, and `documentElement.scrollWidth` was `485` (no horizontal overflow against the 500px layout viewport).
- No product/source edit resulted from this review. The two earlier temporary Madadrang catalog-url edits were reverted before this report was written.
