# Independent admin localization review

Reviewed on 2026-10-08 against the current workspace, including the 5,948-resource pack and local JalaliDatePicker 1.0.0 integration.

## Result

No remaining actionable P1/P2 findings were identified in this final source review. This is a code review, not a claim that every plugin or admin screen has been exercised.

## Evidence

- `AdminLanguageWorkContext` stores the admin choice in the `PersianAdmin.LanguageId` generic attribute and `.Nop.AdminLanguage` cookie. Public requests delegate to the native work context; the admin setter does not write the public language attribute/cookie.
- The header selector uses the admin `Common.SetLanguage` route. Its return URL passes the existing local-URL check.
- `scripts/set-admin-language.ps1 -Mode English` writes the same content-root override file read by the work context. Requests consult the override before the saved preference. `-Mode Automatic` restores normal preference resolution.
- The resource startup consults the recovery override before reading the XML pack. Missing pack files log a warning; a forced-English or malformed recovery flag skips installation. Thus recovery does not require a working Persian page.
- Translation updates compare each existing resource hash with its prior installed baseline. Edited values are preserved when they differ from that baseline. On the first deployment only, exact copies of English seed values may be replaced; other unmatched values remain untouched.
- Missing Persian resources fall back to English on admin requests. Public localization calls retain the native behavior.
- `AdminTransferCalendarAttribute` excludes controllers in external assemblies before conversion or mandatory calendar validation. Built-in XLSX conversion records calendar/date-cell metadata, and services/database values remain Gregorian.
- Decimal and double editors have `step="any"` unless an explicit step is provided. Invalid Persian input clears its native backing value and blocks search. Disabled/read-only changes synchronize the visible companion and picker control.
- The picker is the pinned local upstream library. Its adapter preserves the original native ID/name/ISO-value contract; technical table columns carry a Latin exemption.
- The table `createdCell` Razor block preserves data-column metadata and avoids literal `<text>` in generated JavaScript.

## Verification boundaries

Earlier independent resource validation passed for the then-current 5,936 resources. The final 5,948-resource coverage/build results and earlier browser checks are recorded by the implementing agents in the main execution report; no additional tests or frontend end-to-end run were performed during this final review, in accordance with the latest user instruction.

Actual PDF download/render inspection is handled separately. Its outcome must be reported before claiming that invoice layout and Persian glyph rendering have been verified.

## Final PDF source follow-up

Reviewed the final PDF fixes without running additional tests or frontend checks:

- `OrderDateUser` applies the existing PDF library's `FixWeakCharacters()` in an RTL admin transfer. This follows the already established invoice-note date treatment and prevents slash-separated year/month/day components from reversing visually.
- Persian digit conversion for catalog price, stock and weight is gated by both an active admin transfer calendar and a Persian PDF language. SKU and other technical catalog fields retain their original strings.
- Order/shipment number label conversion uses the same admin/Persian gate. Other PDF languages and public requests without a transfer calendar retain their native presentation.
- `Pdf.SubTotal` now reads «جمع اقلام», which describes the subtotal without introducing a calculation change.

No new actionable P1/P2 issue was identified in these PDF source changes. The PDF agent must confirm the corrected date order and glyph/layout appearance in the regenerated artifacts; this review does not substitute for that rendering evidence.

Final small PDF adjustment also reviewed: catalog product-number prefixes convert to Persian digits only when an admin transfer is active and the PDF language is Persian. Catalog price/weight/SKU direction protection uses `FixWeakCharacters()` only for active RTL admin transfers. This changes presentation order rather than SKU contents; public PDF generation is outside that gate. No additional issue or test was introduced by this follow-up. The parent reports a successful build with zero errors and one pre-existing generated EUROPA warning.

## Download QA corrections follow-up

Reviewed subsequent fixes prompted by inspection of actual downloaded files, without adding backend or frontend tests:

- Both XML calendar variants are now serialized as UTF-8 without the native mismatched UTF-16 declaration. The Gregorian branch leaves element values intact; the Jalali branch additionally converts date values and records calendar metadata. External plugin controller exports still bypass this filter.
- Sales-report `Summary` now uses `ReportSummary` only while an admin transfer calendar is active. The implementation matches the native grouping enum (`Day=0`, `Week=1`, `Month=2`). Day/week dates convert consistently with typed `SummaryDate`; monthly Jalali labels show the converted first/last days of the original Gregorian aggregation month. No change to aggregation boundaries or report totals is made.
- The Persian presentation worksheet converts summary-label digits; the original data worksheet remains available for analysis.
- The final catalog price/weight/SKU direction treatment supersedes the earlier weak-character-only implementation with paired Unicode LTR embedding controls around the complete value. Its gate remains active admin transfer plus RTL language; SKU character content is retained.
- The packaging verification seed checks `current_database()` against `nop_stationery_dev`, uses the marked verification order/item, and avoids duplicate shipment/item insertion. It creates an unshipped local fixture rather than completing a real shipment.

No new actionable P1/P2 source issue was identified. Actual regenerated download/render findings belong in the execution/download QA report.
