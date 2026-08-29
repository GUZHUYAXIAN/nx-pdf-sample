# Gate 3 — Batch Modes and Failure Paths on Disposable Copies

- Date: 2026-08-30 (rerun after the SOL review repairs; original run
  2026-08-29 during Task 11)
- Scope: Task 11 of the approved implementation plan, plus the EV-01/EV-02/
  EV-03 and CR-09 repair items from
  `docs/handoffs/2026-08-30-sol-code-review-findings.md`.
- Privacy: all fixtures and raw run artifacts live under gitignored
  `artifacts/gate-3-fixtures/`. No private names, paths, or hashes are
  committed. Originals were never touched.

## Method

`tools/run-gate-3.ps1` DISCOVERS the private drawing/model pair from the
gitignored `samples\private` directory (exactly one `DWG_*.prt` plus one
model; env-var override `NXPDF_PRIVATE_DRAWING`/`NXPDF_PRIVATE_MODEL`
supported) — real sample names are never written into tracked files. It then
copies the pair into fresh disposable fixtures (pair, nested/a, nested/b,
dup1, dup2, puremodel, missingdep, fresh, fresh3) under
`artifacts/gate-3-fixtures/<guid>/` and drives the REAL product path
headlessly: `tools/NxDrawingPdfExporter.Gate3Driver` (a validation-only host,
not part of the release) runs the production `ApplicationController` — real
NX detection, real worker launch through `run_managed.exe`, real managed PDF
validation (`PdfSharpInspector`), and real transactional publication
(`SafeOutputPublisher`). It never launches a second NX session; each scenario
is one sequential worker run.

Before any NX work the harness also runs the CR-09 privacy scan: `git grep`
over tracked files (excluding the user-approved design specification text)
must find zero hits for the discovered private base names.

## Scenario matrix (all PASS on 2026-08-30)

| # | Scenario | Expected outcome |
|---|---|---|
| S1 | Manual selection (drawing + model + model) beside-source, skip | Success for the drawing, PureModel for the two models; PDF header/page count verified via PdfGate |
| S2 | Current-folder scan after S1 | SkippedExisting without re-export |
| S3 | Recursive scan of nested folders | 2 Success (drawings) + 2 PureModel (models discovered legitimately) |
| S4 | Unified output directory | Success, PDF produced in unified dir, page count verified |
| S5 | Missing-dependency file followed by a FRESH drawing with no pre-existing PDF (EV-01) | Failed (with user-readable reason, no temp leftover) then a REAL `Success` export; the new PDF's header and page count verified via PdfGate |
| S6 | Safe overwrite with pre-seeded old target | Overwritten; target bytes changed |
| S7 | Two same-basename drawings into unified dir | Whole run blocked; all items NameConflict; worker never launched |
| S8 | Overwrite onto a locked (FileShare.None) target | PublishFailed; original SHA-256 unchanged; no temp/backup leftovers |
| S9 | Cancellation flag present before the first file | JobResult.Cancelled true; item Cancelled; no output produced |
| S10 | Cancellation requested DURING item 1 (EV-02) | Harness polls for item 1's temp PDF and writes the cancel flag the moment it appears (item 1 mid-plot). Item 1 finishes atomically with Status=Success and a valid temp PDF; item 2 is `Cancelled` (status 6) with no temp file and no output; `result.json` reports `Cancelled=true` |
| S11 | Real no-valid-sheet PRT followed by a valid drawing (EV-03) | `NoValidSheets` for the fixture (no PDF, no temp leftover), then a real `Success` export with page-count-verified PDF |

Notes on the new scenarios:

- **S10 (EV-02).** The cancel flag is written while item 1's export is in
  progress (its temp PDF exists), proving the approved "finish the current
  atomic operation, stop before the next file" boundary on a real NX run.
  Residual race: the flag is written within ~10 ms of temp-file creation;
  item 1's remaining plot/report/snapshot work is far longer. The App-side
  publication boundary is covered separately by
  `ApplicationControllerTests.Cancel_DuringPublication_PublishesCurrentFileThenCancelsRest`.
  One sequential NX session by construction (a single `run_managed.exe`
  launch for the job).
- **S11 (EV-03).** The fixture is created by
  `tools/NxDrawingPdfExporter.FixtureBuilder` (validation-only tool): a NEW
  synthetic part with one A4 sheet and zero drafting views, saved through the
  verified `run_managed.exe` path. The tool saves only the brand-new part it
  creates; it never touches sample PRTs. The file-level `NoValidSheet`
  behavior is now proven on real NX, closing the previous unit-test-only gap.

## Defect found and fixed during the 2026-08-29 gate

The first full run exposed a real product bug: a drawing PRT with a missing
model dependency exported successfully with stale views instead of failing.
Per the approved design, missing dependencies must fail that PRT. Fixes:

- `NxFileJobRunner.ExportSingle` now fails the PRT when the load status
  reports any unloaded component (message carries the NX load diagnostics).
- `ExportReportRules` now rejects a successful report that carries load
  diagnostics (`Validate_SuccessfulReportWithLoadDiagnostics_ReportsIssue`,
  written RED first).

The gate was rerun in full after the fix and passed; the 2026-08-30 rerun
confirmed it again.

## Protection checks (after the full matrix, 2026-08-30 run)

- CR-09 privacy scan before NX work: zero tracked-file hits for the private
  base names.
- Original sample pair: before/after SHA-256 equal.
- NX install fingerprints (`run_managed.exe`, `NXOpen.dll` length + mtime):
  unchanged.
- `rg -n "\.Save\(|SaveAs\(|SaveComponents|PartSave" src`: no production
  NX save calls.
- Fixtures and runs remain only under gitignored artifacts.

## Static verification after the repair commits

- Contracts tests: 19 passed; Core tests: 94 passed; App tests: 31 passed
  (all suites re-run after the CR-01…CR-06 fixes).
- Release build: 0 warnings, 0 errors (with the new Release symbol policy).
- Run log: `artifacts/gate-3-run-20260830-015258.log` (gitignored).
