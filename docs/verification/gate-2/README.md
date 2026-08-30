# Gate 2 — Selected-Sheet NX Export and Sample PDF Evidence

- Date: 2026-08-29
- Scope: Task 8 of the approved implementation plan (Tasks 7 evidence is in
  the commit history; this document covers the live export gate).
- Privacy: no private sheet names, source paths, source hashes, Tag values,
  or drawing content are committed. All raw artifacts, name maps, renders,
  and hashes stay under gitignored `artifacts/`.

## Worker export path

`NxDrawingPdfExporter.Worker.exe --export <drawing-prt> <temp-pdf> <report>`:

1. `SourceFileGuard` captures full path, length, UTC mtime, SHA-256.
2. One candidate is opened with `Session.Parts.OpenDisplay`; load status is
   captured as diagnostics.
3. Sheets are enumerated in public `DrawingSheets` collection order;
   `GetDraftingViews().Length >= 1` is the only exportable rule
   (`SheetSelectionService`).
4. Each selected sheet is opened and its out-of-date views updated through
   the verified `DraftingViewCollection.UpdateViews(ViewUpdateOption, sheet)`
   overload. Any update failure fails the PRT; it never degrades into a
   skipped/blank page.
5. One `PrintPDFBuilder.Commit()` writes all selected sheets to the temp PDF:
   `BlackOnWhite`, `FullScale`, empty watermark, `Append = false`, no common
   scale, no X/Y dimensions (original sheet sizes preserved).
6. The source guard re-checks the PRT; `Close(CloseWholeTree.True,
   CloseModified.CloseModified, null)` closes without saving.

### CloseModified finding

The first live attempt failed with `NXException: Modified part not saved`
thrown by `BasePart.Close` inside the close step. Root cause: view updates
mark the drawing part modified in memory; NX 10 throws when closing the whole
tree with `DontCloseModified` in that state. The fix uses
`CloseModified.CloseModified` ("close all modified parts"), which discards
the in-memory view updates and never saves. Evidence of the failed attempt
and the stack trace stayed in gitignored run artifacts.

### Exit-code finding

`run_managed.exe` returned exit code 0 even when the worker reported failure
(verified once during diagnosis). Harnesses therefore treat `result.json` /
`export-report.json` structural validation as the authoritative outcome and
never rely on the exit code alone.

## Harness (`tools/run-sample-export.ps1`)

- Builds Worker and `tools/NxDrawingPdfExporter.PdfGate` (a validation-only
  host that loads the production `PdfSharpInspector`; not part of the product
  or release, and not in the solution file).
- Records SHA-256 for the drawing PRT and its associated model before
  launching NX and again in `finally` for every outcome (success, timeout,
  nonzero exit, invalid report). Any mismatch or capture error is a hard
  failure. Timeout handling kills and waits only for the process it started.
- Structurally validates the export report: success, no failure text,
  `SourceUnchanged`, per-index order coherence (`ExportOrderIndex` and
  `NativeIndex` equal array positions), token validity/uniqueness,
  selected sheets carry at least one drafting view, skipped sheets carry
  zero, and Selected/Skipped counts consistent.
- Inspects the temp PDF through `PdfSharpInspector` (managed, offline): PDF
  header, page count equal to selected count, and every page MediaBox equal
  to the same-order NX sheet size (mm→pt conversion, 1.5 pt tolerance).
- The gate host runs on the repo-local .NET 10 SDK via process-local
  `DOTNET_ROOT`; no machine configuration changes.

## Visual QA

Rendering uses the Windows built-in `Windows.Data.Pdf` component via
`tools/render-verification-pdf.ps1` (no installation, no download, no
Python). Renderer identity recorded in the run's `render-summary.json`:
file version 10.0.26100.8875, OS 10.0.26200.0, 150 dpi.

The exported page was checked for: white background, black lines, complete
border, title block, general technical requirements text, dimensions,
orientation, correct view geometry, and no watermark. The same check was
performed on the private human reference PDF; the exported page matches it
in content, sheet size, and style. Per the plan, binary equality is not
required and no reference filename/content is recorded here. Rendered PNGs
remain only in gitignored artifacts because they contain confidential
drawing content.

## Live-gate result

PASS on 2026-08-29 with the protected original samples:

- export report success: true; 9 sheets in public `DrawingSheets` order;
  1 exportable (2 drafting views, A2 594×420 mm); 8 skipped with zero
  drafting views; load diagnostics empty; `SourceUnchanged` true;
  elapsed ≈ 6.1 s.
- PDF: header valid, 1 page, MediaBox 1683.78 × 1190.55 pt = 594 × 420 mm
  within tolerance of the NX sheet size; visual QA passed as above.
- Source protection: before/after SHA-256 equal for both the drawing PRT and
  the associated model.
- NX isolation: the user's original NX GUI session (ugraf.exe, PID 2376,
  holding an unrelated modified document) was observed before the run and
  remained running and untouched; the harness launched and managed only its
  own `run_managed.exe` process.

An additional disposable-copy rehearsal (copy of both PRTs under gitignored
artifacts) reproduced the same result before the protected run; both copies
were deleted afterward.

## Static verification at Task 8 commit

- Contracts tests: 15 passed; Core tests: 70 passed (10 new export-report
  rule tests written RED first); App tests: 5 passed.
- Worker Debug build: 0 warnings, 0 errors; no Siemens DLLs in output.
- Task 7 transactional publication (`SafeOutputPublisher` + managed
  `PdfSharpInspector`) is in place and tested for the App-side validation
  that will wrap this temp PDF in the product flow.
