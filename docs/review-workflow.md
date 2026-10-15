# Reconstruction Review Workflow (v0.3.0 preview)

1. Import an Xactimate mitigation PDF.
2. Inspect source line numbers, rooms, descriptions and quantities.
3. Switch to reconstruction candidates.
4. Edit proposed work, quantities and review notes; mark reviewed items Approved.
5. Select **Save review draft** to export a JSON draft. This is not an Xactimate ESX or a priced PDF estimate.

## Important limitations
- The PDF parser is a first-pass text extractor and has not been confirmed to capture all 71 sample items in the WinForms runtime. Multi-column layout needs position-aware validation.
- Approvals and quantities are saved in JSON only. Draft reopening, change history and estimate PDF generation are not yet implemented.
- No Xactimate catalog selector or regional price data is included.
- This version is not production ready; GitHub Actions build verification is pending.
- Before use for claims, validate against original Xactimate estimate, measurements, field conditions, and authorized price lists.
