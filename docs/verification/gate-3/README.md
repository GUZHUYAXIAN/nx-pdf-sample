# Gate 3 — Batch Modes and Failure Paths on Disposable Copies

- Date: 2026-08-29
- Scope: Task 11 of the approved implementation plan.
- Privacy: all fixtures and raw run artifacts live under gitignored
  `artifacts/gate-3-fixtures/`. No private names, paths, or hashes are
  committed. Originals were never touched.

## Method

`tools/run-gate-3.ps1` copies the private drawing/model pair into fresh
disposable fixtures (pair, nested/a, nested/b, dup1, dup2, puremodel,
missingdep) under `artifacts/gate-3-fixtures/<guid>/` and drives the REAL
product path headlessly: `tools/NxDrawingPdfExporter.Gate3Driver` (a
validation-only host, not part of the release) runs the production
`ApplicationController` — real NX detection, real worker launch through
`run_managed.exe`, real managed PDF validation (`PdfSharpInspector`), and
real transactional publication (`SafeOutputPublisher`). It never launches a
second NX session; each scenario is one sequential worker run.

## Scenario matrix (all PASS)

| # | Scenario | Expected outcome |
|---|---|---|
| S1 | Manual selection (drawing + model + model) beside-source, skip | Success for the drawing, PureModel for the two models |
| S2 | Current-folder scan after S1 | SkippedExisting without re-export |
| S3 | Recursive scan of nested folders | 2 Success (drawings) + 2 PureModel (models discovered legitimately) |
| S4 | Unified output directory | Success, PDF produced in unified dir |
| S5 | Missing-dependency file followed by a normal file | Failed (with user-readable reason) then continuation; no temp PDF leftover |
| S6 | Safe overwrite with pre-seeded old target | Overwritten; target bytes changed; valid PDF |
| S7 | Two same-basename drawings into unified dir | Whole run blocked; all items NameConflict; worker never launched |
| S8 | Overwrite onto a locked (FileShare.None) target | PublishFailed; original SHA-256 unchanged; no temp/backup leftovers |
| S9 | Cancellation flag present before the first file | JobResult.Cancelled true; item Cancelled; no output produced |

S10 (replacement interruption restoring old bytes) is covered deterministically
by `SafeOutputPublisherTests.Publish_ReplacementInterruption_RestoresOriginalBytes`
with an `IFileReplacer` test double, and S8 proves the equivalent Windows
integration path against a genuinely locked target. A dedicated no-valid-sheet
PRT fixture cannot be authored without interactive NX; the rule is covered by
`SheetSelectionServiceTests` and worker code paths, and the live sample's eight
zero-view template sheets (Gate 2) exercise the same classification.

## Defect found and fixed during this gate

The first full run exposed a real product bug: a drawing PRT with a missing
model dependency exported successfully with stale views instead of failing.
Per the approved design, missing dependencies must fail that PRT. Fixes:

- `NxFileJobRunner.ExportSingle` now fails the PRT when the load status
  reports any unloaded component (message carries the NX load diagnostics).
- `ExportReportRules` now rejects a successful report that carries load
  diagnostics (`Validate_SuccessfulReportWithLoadDiagnostics_ReportsIssue`,
  written RED first).

The gate was rerun in full after the fix and passed.

## Protection checks (after the full matrix)

- Original sample pair: before/after SHA-256 equal.
- NX install fingerprints (`run_managed.exe`, `NXOpen.dll` length + mtime):
  unchanged.
- `rg -n "\.Save\(|SaveAs\(|SaveComponents|PartSave" src`: no production
  NX save calls.
- Fixtures and runs remain only under gitignored artifacts.

## Static verification at Task 11 commit

- Contracts tests: 19 passed; Core tests: 84 passed; App tests: 24 passed.
- Solution Debug build: 0 warnings, 0 errors.
