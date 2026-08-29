# Release Verification (Task 12)

- Date: 2026-08-29
- Scope: packaging, forbidden-content inspection, and clean-extraction QA.
- Privacy: package lives under gitignored `artifacts/release/`; QA fixtures
  under gitignored `artifacts/release-qa/`. No private names, paths, or
  hashes are committed.

## Package layout (`artifacts/release/package/`)

```text
NX图纸批量导出工具.exe   GUI: win-x64 self-contained single file (~113 MB)
THIRD-PARTY-NOTICES.txt  PDFsharp 6.2.4 MIT notice
说明.txt                 concise Chinese usage notes
worker\
  NxDrawingPdfExporter.Worker.exe       net48 x64
  NxDrawingPdfExporter.Worker.exe.config
  NxDrawingPdfExporter.Contracts.dll
  NxDrawingPdfExporter.Core.dll
```

Built by `tools/publish-portable.ps1`: Release test + build of the whole
solution first, then `dotnet publish -r win-x64 --self-contained true
/p:PublishSingleFile=true` for the GUI and a plain net48 file copy for the
Worker (PDBs excluded). All NuGet assets resolved from the repo-local cache;
no network access at any point.

## Forbidden-content inspection

`tools/inspect-release.ps1` scans every package file and fails on Siemens
DLLs (`NXOpen*.dll`), `.prt`/`.pdf` payloads, source files, logs/temp/PDBs,
secrets, private name maps, and developer-absolute paths inside text
payloads. Result: PASS over 7 files. The SHA-256 manifest is written to
`artifacts/release/manifest.sha256` (gitignored, regenerated per build).

## Clean-extraction QA

The package was copied to a fresh directory outside the repository
(`%LOCALAPPDATA%\Temp\nxpdf-clean-extract-<timestamp>\`) and launched there:

- The GUI started successfully as a self-contained single file; the machine
  has no .NET 10 runtime installed (only 5.0/6.0), proving the GUI needs no
  system-installed modern .NET, no Python, and no Visual Studio.
- NX detection succeeded against the verified local install and the full
  single-window UI rendered with correct Chinese text and no clipped
  controls (100% DPI; see limitations).

A controlled live export was then run with the PACKAGED worker through the
verified `run_managed.exe` path on a disposable fixture copy:

- export report: Success (status 0);
- PDF: valid header, 1 page, MediaBox 1683.78 × 1190.55 pt = A2 594 × 420 mm,
  byte-identical size to the Gate 2 export (163,102 bytes);
- original sample pair SHA-256: before/after equal.

## Offline evidence

The product runtime contains no networking code: a source-tree audit for
`HttpClient`, `WebRequest`, `WebClient`, `Dns.`, `DownloadString`, and
`ServicePoint` finds no hits in product `.cs` files (only framework file
lists inside build artifacts mention such assembly names). The machine's
network adapter was deliberately not disabled because changing system
network configuration is outside the authorized boundaries; the static
audit plus the fully offline build/publish process stand in for it.

## Final audits

- Release test + build: 0 failed tests, 0 warnings, 0 errors.
- Placeholder audit
  (`rg "TODO|TBD|NotImplementedException|throw new Exception\(\)|catch\s*\{\s*\}"`
  over src/tests/tools): no hits.
- Source protection: all live gates re-verified the protected pair's
  SHA-256 before/after; NX install fingerprints unchanged throughout.
