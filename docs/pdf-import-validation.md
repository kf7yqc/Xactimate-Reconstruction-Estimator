# PDF import validation — 129 Artist View Road

## Independent verification (2026-10-15)
A separate PyMuPDF script was run against the user-supplied 11-page PDF in the working environment. Pages 2–6 contained **71 of 71** sequential line numbers, with no duplicates or missing numbers.

| Section | Count |
|---|---:|
| General (page 2) | 13 |
| Bathroom (pages 3–4) | 34 |
| Laundry Room (pages 5–6) | 24 |

Verified source quantities: item 38 = 38.72 SF, item 55 = 69.17 LF, items 61 and 62 = 69.17 LF each, items 63 and 64 = 185.89 SF each.

**Important:** The PyMuPDF extraction result is not a passing test of the Windows PdfPig importer. The PDF itself is not stored in the repository because it contains private customer/job information.

## Acceptance criteria for Windows app
- Build succeeds in GitHub Actions on Windows.
- PdfPig parser extracts all 71 numbered items from the sample.
- Room assignments match the table above.
- The six verified quantities match.
- Wrapped descriptions are reconstructed correctly before rules-based mapping.
- Missing or duplicate numbers produce warnings; no-priced-estimate output is generated until source is reviewed.
- Scope review exports preserve editable quantities and review status.

The importer now attempts position-aware row extraction. Build and full sample validation are still pending.
