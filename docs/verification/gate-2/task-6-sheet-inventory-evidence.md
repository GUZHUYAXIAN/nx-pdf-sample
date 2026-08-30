# Gate 2 Task 6 — NX Drawing Sheet Inventory Evidence

- Date: 2026-08-29
- Scope: Task 6 (safe sheet inspection) of the approved implementation plan
- Privacy: no private sheet names, source paths, source hashes, Tag values, or
  drawing content are stored here. Raw run artifacts remain gitignored.

## Approved order contract

On 2026-08-29 the user approved a clarification of the V1 order contract:
export sheets in the order returned by the NX 10 public NXOpen
`workPart.DrawingSheets` collection. The visible Navigator's timestamp or
dependency display order is a GUI preference and is outside this contract.
No verified NX 10 public API was found that exposes that visible tree order.

The previous UF `AskDrawings` adapter was removed. Local protected evidence
had already shown that UF and `DrawingSheets` returned the same sequence while
the visible timestamp-sorted tree differed. UF therefore added no supported
ordering information. Alphabetical, natural-name, numeric-name, and Tag-value
sorting remain forbidden.

## Method

### Source protection on every live attempt

`tools/run-sheet-inventory.ps1` records SHA-256 for the drawing PRT and its
associated model before launching NX, then records both again in a `finally`
block. This covers success, timeout, nonzero exit, malformed report, and
exceptions. On timeout it terminates and waits only for the process object it
started. A hash capture error or mismatch is a hard failure.

The Worker separately captures full path, length, UTC modification time, and
SHA-256 through `SourceFileGuard`; it opens exactly one candidate using
`Session.Parts.OpenDisplay`, captures load diagnostics, and closes with
`DontCloseModified`. Production source contains no NX Save/SaveAs calls.

### Report and export-order rules

For each public `DrawingSheets` item, `NxSheetInventory` records:

- `ExportOrderIndex`, equal to the report-array position;
- `NativeIndex`, equal to the same public-collection position and retained as
  provenance evidence;
- nonnegative actual Drafting View count;
- positive finite sheet length and height;
- units and a per-run salted privacy-safe name token.

Both Core and the independent PowerShell harness reject missing, malformed,
unsuccessful, or structurally incoherent reports. The tracked JSON schema uses
the same field contract. The token-to-name map exists only in the ignored run
directory and is never printed or committed.

### Exportable-page rule

A sheet is exportable only when `GetDraftingViews().Length >= 1`. A sheet with
no actual Drafting View is skipped even if it contains borders, title blocks,
general technical requirements, or tables. Load/update failures fail the PRT
instead of being classified as blank/template pages.

## Verification evidence

Static verification on 2026-08-29:

- Core tests: 45 passed, 0 failed;
- Contracts tests: 15 passed, 0 failed;
- inventory harness: 21 checks, 0 failures;
- Worker Debug build: 0 warnings, 0 errors;
- copied Siemens DLLs in Worker output: 0;
- production NX Save API hits: 0;
- obsolete `NavigatorIndex`, UF adapter, and `AskDrawings` hits in runtime,
  tests, and tools: 0;
- tracked forbidden private/artifact/binary files: 0.

## Live-gate result

PASS. The user explicitly authorized a separate safe NX session while the
existing GUI session retained an unrelated modified document. The protected
inventory harness launched and managed only its own process; afterward the
original NX PID was still running and the independent validation process had
exited.

The fresh report succeeded with nine sheets: one exportable sheet and eight
skipped sheets. Load diagnostics were empty. `ExportOrderIndex` and
`NativeIndex` each matched report-array positions 0..8 item-for-item, and no
legacy `NavigatorIndex` field was present. The ignored token-to-name map was
created only under the run-artifact directory.

Before/after SHA-256 matched for both the protected drawing PRT and its
associated model. No hash, private name, source path, Tag value, or drawing
content is committed. Gate 2 Task 6 is complete; PDF export remains outside
this task and was not started.
