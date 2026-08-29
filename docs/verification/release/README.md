# Release Verification

- Date: 2026-08-30 (rebuilt after the SOL review repairs; original build
  2026-08-29)
- Scope: packaging, forbidden-content inspection, manifest integrity, and
  clean-extraction QA.
- Privacy: package lives under gitignored `artifacts/release/`; QA fixtures
  under gitignored `artifacts/release-qa/`. No private names, paths, or
  hashes are committed.

## Package layout (`artifacts/release/package/`, 7 files)

```text
NX图纸批量导出工具.exe   GUI: win-x64 self-contained single file (117,881,061 bytes)
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
Worker (PDBs excluded). Since the repair round, Release builds use
`DebugType=none` plus a `PathMap` of the repository root to `/nxpdf/`
(CR-07): the previous package embedded the developer checkout path in the
GUI executable and the worker binaries' symbol/CodeView metadata; the
rebuilt payloads contain none.

## Forbidden-content inspection

`tools/inspect-release.ps1` scans every package file by name (Siemens DLLs,
`.prt`/`.pdf`, sources, logs/temp/PDBs, secrets, private name maps) AND
scans every payload's raw bytes — both ASCII and UTF-16LE forms — for
developer paths (`E:\Codex`, `nx-step-launcher`, any `D:\Program Files`
other than the approved NX-root runtime constant, and user-profile drive
paths). The exact verified NX install root
`D:\Program Files\Siemens\NX 10.0` is an approved runtime constant required
by fail-closed detection (see the approved design) and is whitelisted
verbatim.

Result on the rebuilt package: **PASS** over 7 files, zero developer-path
hits. A self-test fixture
(`tools/test-inspect-release.ps1`) proves the inspector rejects a binary
payload embedding a forbidden path (ASCII and UTF-16LE), round-trips
Chinese/space filenames, and detects a single flipped byte.

## SHA-256 manifest (CR-08)

The manifest is written as UTF-8 without BOM
(`artifacts/release/manifest.sha256`, gitignored) and is immediately
verified entry-by-entry against the package: every relative path (including
the Chinese filenames `NX图纸批量导出工具.exe` and `说明.txt`) round-trips
exactly, coverage is one-to-one, and every hash matches. The verifier mode
(`-VerifyManifestOnly`) re-checks any delivered copy against its manifest.

## Clean-extraction QA

The package was copied to a fresh directory outside the repository
(`%TEMP%\nxpdf-clean-extract-<timestamp>\`) and the manifest was re-verified
against the extracted copy (7/7 files, hashes equal).

A controlled live export was then run with the PACKAGED worker through the
verified `run_managed.exe` path on a disposable fixture copy of the private
pair (drawing + model, so component loading succeeds):

- export report: Success (status 0), `FatalError` null;
- PDF: valid header, 1 page, MediaBox 1683.78 × 1190.55 pt = A2 594 × 420 mm,
  length 163,102 bytes (measured on this build; no binary-equality claim is
  made against other runs — acceptance is semantic: header, page count,
  MediaBox, rendered visual QA);
- original sample pair SHA-256: before/after equal.

The same run also demonstrated the missing-dependency rule end-to-end: a
first attempt with a drawing-only fixture failed with
"模型依赖未完全加载" (status 7), produced no PDF, and left no temp file.

## Offline evidence

The product runtime contains no networking code: a source-tree audit for
`HttpClient`, `WebRequest`, `WebClient`, `Dns.`, `DownloadString`, and
`ServicePoint` finds no hits in product `.cs` files (only framework file
lists inside build artifacts mention such assembly names), and the entire
build/publish/test pipeline ran against the repo-local NuGet cache without
network access. A physically disconnected clean-extraction run remains an
open acceptance item (EV-05) and must be performed by the user; the static
audit is supporting evidence, not a substitute.

## Final audits

- Release test + build: 0 failed tests, 0 warnings, 0 errors.
- Placeholder audit
  (`rg "TODO|TBD|NotImplementedException|throw new Exception\(\)|catch\s*\{\s*\}"`
  over src/tests/tools): no hits.
- Source protection: Gate 3 and the release-QA export re-verified the
  protected pair's SHA-256 before/after; NX install fingerprints unchanged.
