# Persian administrator resources

`fa-IR.admin.xml` targets the repository's nopCommerce 5.00 English resources.
It covers every upstream `Admin.*`, `Enums.*`, `ActivityLog.*`, `Pdf.*`,
`PDFInvoice.*`, `Security.Permission.*`, `Common.*`, and `Plugins.FriendlyName.*`
resource, plus the shared login page and its validation resources. Customer
email message templates are outside this pack.

The baseline was translated from the current source text using Google Translate
full sentences, with formatted placeholders, message tokens, URLs and HTML tags
preserved. HTML whose protection markers could not be restored was translated
as visible text fragments, retaining the original tags. This is a complete
translation baseline, **not a claim that every sentence has been reviewed by a
human translator**. The curated glossary in `reviewed-phrases.json` contains the
explicitly reviewed UI and commerce wording; `coverage.json` lists the exact
resource occurrences it covers. Common controls, dashboard, commerce terms,
invoice/slip labels and login errors have curated translations. Technical
identifiers and provider names such as SKU, API and PostgreSQL stay Latin.

Source: the repository's `defaultResources.nopres.xml`, distributed under the
repository's nopCommerce license; see the root license and copyright notices.
No translation text from the older third-party language pack was incorporated.
The official nopCommerce community pack was investigated at
<https://www.nopcommerce.com/en/translations> but its download was inaccessible
behind a browser verification challenge; its advertised percentage was not used
as evidence for this pack's coverage.

`PersianAdminResourcesStartup` creates an RTL `fa-IR` language after the existing
languages in display order, before the supported cultures are built. English
stays present. Custom English labels are inserted from `en-US.admin.xml` only
when absent.

For Persian updates, a SHA-256 baseline ledger is stored as a GenericAttribute
on the Persian Language entity. Missing resources are inserted. Existing values
are changed only when their current hash matches the previous installed pack's
hash. On the first deployment only, a value exactly equal to the matching English
resource is treated as a copied seed and replaced. Other preexisting values
without a ledger match, or values edited through the language resource editor,
are preserved. Removed pack entries are not deleted.
Do not import the entire XML through nopCommerce's ordinary overwrite import if
you want this preservation policy; deploy the file and restart instead.

Validate future edits with:

```powershell
python scripts/validate-admin-localization.py
```

The validator checks exact resource coverage, duplicate keys, placeholders,
message tokens, original HTML tags and remaining translation protection tokens.
It does not certify translation fluency; inspect modified wording in its screen
context and review the downloaded PDF and spreadsheet outputs separately.
