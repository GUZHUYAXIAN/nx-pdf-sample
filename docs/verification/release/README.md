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
scans every payload's raw bytes — both ASCII and UTF-16LE forms.

**Path policy (explicit, reviewed; second-review finding 2).** The package
must not reveal this developer's machine, and every embedded absolute
build/source path must be accounted for:

- Forbidden outright: the developer checkout (`E:\Codex`), the offline
  toolchain project (`nx-step-launcher`), any `D:\Program Files` path other
  than the approved NX root, and user-profile drive paths (`X:\Users\`).
- Generic build/source paths — candidates containing source/build
  indicators (`\src\`, `\source\`, `\obj\`, `.cs`, `.cpp`, `.hpp`, `.pdb`,
  `.vb`, `.rs`) — must start with a REVIEWED upstream prefix; anything else
  fails the gate and forces a fresh review. The reviewed allowlist:
  - `D:\Program Files\Siemens\NX 10.0` — the approved runtime constant
    required by fail-closed detection (see the approved design);
  - `D:\a\_work\` — Microsoft's official .NET runtime CI root, inherent to
    the official `microsoft.netcore.app.runtime.win-x64` pack and identical
    for every consumer;
  - `D:\repos\empira\` — PDFsharp's upstream dev root, inherent to the
    official PDFsharp 6.2.4 NuGet binary.
- Binary noise that merely resembles `X:\...` without any source/build
  indicator is not treated as a path (the runtime binary contains random
  byte sequences that would otherwise make the gate unusable).

A self-test fixture (`tools/test-inspect-release.ps1`, 7 scenarios) proves:
a binary embedding the developer checkout path is rejected in both ASCII
and UTF-16LE encodings; the manifest round-trips Chinese/space filenames
and detects a single flipped byte; reviewed upstream paths are allowed;
unknown source paths and user-profile paths are rejected; and binary noise
without indicators is not misreported.

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
  length 163,101 bytes (measured on the rebuilt package; export lengths
  vary by ±1 byte between runs, so no binary-equality claim is made —
  acceptance is semantic: header, page count, MediaBox, rendered visual QA);
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
network access. A physically disconnected clean-extraction run was not
performed; the user accepted this as a v1.0.0 release limitation and deferred
it to GitHub Issue #3. The static audit remains supporting evidence, not a
substitute for that future run.

## Final audits

- Release tests: 149 passed, 0 failed. Build completed with 0 errors; the
  Worker test ProjectReference emits the known NU1702 cross-target-framework
  compatibility warning (net10 test harness referencing the net48 Worker).
- Placeholder audit
  (`rg "TODO|TBD|NotImplementedException|throw new Exception\(\)|catch\s*\{\s*\}"`
  over src/tests/tools): no hits.
- Source protection: Gate 3 and the release-QA export re-verified the
  protected pair's SHA-256 before/after; NX install fingerprints unchanged.
