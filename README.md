# Xactimate Reconstruction Estimator — Windows tester preview

A Windows Forms (.NET 10) review tool that imports Xactimate-style mitigation PDFs and suggests **unpriced** reconstruction scope candidates. All quantities and scope require estimator review. This is not an official Xactimate integration.

## Download a Windows test build

1. Open **[Releases](https://github.com/kf7yqc/Xactimate-Reconstruction-Estimator/releases)**.
2. Select the newest **Windows Tester Preview** prerelease.
3. Download `Xactimate-Reconstruction-Estimator-win-x64-preview.zip`.
4. Right-click the ZIP > **Extract All**.
5. Launch `Xre.App.exe` from the extracted folder. Windows may ask you to confirm running an unsigned preview.
6. Select **Import mitigation PDF**, review source items, then switch to reconstruction candidates. Edit quantities, add notes, approve reviewed items, and save a JSON draft.
7. Test **Export CSV** and **Export scope report**. HTML scope reports can be printed to PDF in a browser.

**If no release is visible:** the Windows build may still be running or may have failed. Open [Actions](https://github.com/kf7yqc/Xactimate-Reconstruction-Estimator/actions/workflows/build.yml) to view the run and its logs. No successful build is claimed until Actions passes.

## What to test

- PDF import displays correct numbered items, quantities and rooms.
- Reconstruction candidates are proposed only where mapping rules recognize the source work.
- Saving/reopening a JSON review retains edits and approvals.
- HTML and CSV exports match the reviewed scope.
- Importing a second file prompts before replacing the current review.

## Limitations

- **No Xactimate regional pricing:** Xactimate Desktop/X1 does not itself grant third-party API access. Prices must come from an authorized source.
- **No native PDF export yet:** printable HTML is provided.
- **No automatic installer/updater yet:** the preview is a ZIP; the notification bell remains a placeholder.
- **No verified production accuracy:** the Windows PdfPig importer must pass a full 71-line sample acceptance test.
- No proprietary source PDFs or customer data are stored in this public repository.

## Building locally

On Windows with .NET 10 SDK:

```powershell
dotnet run --project tests/Xre.SmokeTests/Xre.SmokeTests.csproj -c Release
dotnet publish src/Xre.App/Xre.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Optionally set `XRE_SAMPLE_PDF` to the path of a privately stored sample estimate before running smoke tests. This adds the real-PDF acceptance checks.

## Release pipeline

Every push to `main` triggers `.github/workflows/build.yml`. After a successful Windows build and smoke tests, GitHub Actions publishes a numbered **prerelease** containing a Windows ZIP and SHA-256 checksum. If a test fails, no new prerelease is published.
