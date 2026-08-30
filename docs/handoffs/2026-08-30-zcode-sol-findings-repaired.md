# SOL Findings Repaired — Handoff for Second Review

- Date: 2026-08-30
- Branch: `feature/nx-drawing-pdf-exporter-v1`
- Review input: local-only `docs/handoffs/2026-08-30-sol-code-review-findings.md`
  (intentionally untracked; HEAD reviewed after history sanitization: `56dcf3c`)
- Repair range for second review: `56dcf3c..1c94404` (5 commits)
- Second-round repairs after the second review: `1c94404..HEAD` (see the
  addendum at the bottom of this file)
- Scope: code/tool repairs with RED→GREEN regression tests, Gate 3 rerun,
  package rebuild and re-verification. No push, no tag, no release. No
  private sample was touched (hashes re-verified per live run).

## Commits (oldest → newest)

| Commit | Content |
|---|---|
| `ef2fcb0` | CR-01/02/03/05: per-run state reset, all-skipped short-circuit, publish-boundary cancellation, RunId ownership + FatalError handling (7 new App tests) |
| `fe2da72` | CR-04: `FatalResultMerger` preserves the durable snapshot on worker fatal error (7 new Core tests); Worker `RunJob` merges instead of overwriting |
| `5bca0e5` | CR-06: final PDF revalidated after overwrite before backup deletion (3 new Core tests) |
| `71efedb` | CR-07/08/09: byte-level release inspection + UTF-8 manifest self-verify + fixture self-test; Release `DebugType=none` + `PathMap`; synthetic test names; Gate 3 private-pair discovery + tracked-file privacy scan; new `FixtureBuilder` tool |
| `1c94404` | Docs: fresh Gate 3/release evidence, corrected claims (EV-06), accurate DPI limitation (EV-04 statement part), README status downgrade (EV-07) |

## Finding → resolution map

### A. Code and release-tool findings

- **CR-01 (P1)** — FIXED. `RunAsync` resets `Results`/`Summary`/
  `ProgressText`/`CurrentRunDirectory` at the accepted start boundary of
  every run. Tests: `Run_SecondRunFailsBeforeLaunch_DoesNotRetainFirstRunSuccessState`,
  `Run_SecondRunInvalidResultJson_KeepsOnlyCurrentRunState`.
- **CR-02 (P1)** — FIXED. All-skipped plans complete in the GUI process;
  no Worker-path check, no run directory, no NX launch. Test:
  `Run_AllTargetsExistWithSkipPolicy_NeverLaunchesWorkerOrCreatesRunDirectory`.
- **CR-03 (P1)** — FIXED. One controller-owned cancellation state observed
  between publish operations; current publish finishes, remaining items
  become `Cancelled` and their run-owned temp PDFs are deleted. Test:
  `Cancel_DuringPublication_PublishesCurrentFileThenCancelsRest` (blocking
  inspector double).
- **CR-04 (P1)** — FIXED. Worker fatal path merges the last trusted durable
  snapshot (same RunId, protocol, task-order prefix) and appends explicit
  `Failed` outcomes for unresolved items; never replaces known results with
  an empty array; nonzero exit preserved. Tests: 7 in
  `FatalResultMergerTests`.
- **CR-05 (P1)** — FIXED. Results from a different RunId are rejected
  wholesale; `FatalError` keeps the trusted per-file prefix, marks
  unresolved items failed with a sanitized batch message, and the terminal
  progress text never claims a normal completion. Tests:
  `Run_ResultFromDifferentRunId_AllItemsFailWithoutPublication`,
  `Run_FatalErrorWithNoFileResults_AllItemsFailWithoutPublication`,
  `Run_FatalErrorAfterOneSuccess_PublishesOnlyTheTrustedPrefix`.
- **CR-06 (P1)** — FIXED. The final target is inspected after
  `ReplaceWithBackup` with the same header/page-count rules; failed
  revalidation restores the backup and reports `PublishFailed` with
  `OriginalPreserved`. Tests: `Publish_ReplacementWritesNonPdfBytes_RestoresOriginal`,
  `Publish_ReplacementWritesWrongPageCount_RestoresOriginal`,
  `Publish_OverwriteExisting_DeletesBackupOnlyAfterFinalInspection`
  (timeline recording proves backup deletion follows final inspection).
- **CR-07 (P1)** — FIXED. `inspect-release.ps1` scans every payload's raw
  bytes (ASCII + UTF-16LE) for `E:\Codex`, `nx-step-launcher`, any
  `D:\Program Files` other than the approved NX-root runtime constant
  (whitelisted verbatim; it is the fail-closed detector's required constant
  from the approved design), and user-profile drive paths. Release builds
  now use `DebugType=none` + `PathMap`→`/nxpdf/`. Empirical result: the
  OLD package's binaries contained the developer checkout path; the
  REBUILT package has zero hits (the first post-rebuild scan caught only
  the approved NX root constant, now whitelisted). Self-test:
  `tools/test-inspect-release.ps1` (dirty binary rejected in both encodings).
- **CR-08 (P1)** — FIXED. Manifest is UTF-8 without BOM and immediately
  verified entry-by-entry (path round-trip incl. Chinese/space names,
  one-to-one coverage, hash equality); `-VerifyManifestOnly` re-verifies any
  copy. Self-test also proves a single flipped byte fails verification.
- **CR-09 (P1)** — FIXED in tracked files. Unit tests use synthetic names;
  Gate 3 discovers the gitignored pair (env override supported) and runs a
  `git grep` privacy scan over tracked files (excluding the user-approved
  design specification text) before any NX work — zero hits required and
  achieved. **Open user decisions (not done implicitly):** (a) the approved
  design spec `docs/superpowers/specs/2026-08-26-…-design.md` still contains
  the real sample names — needs the user's explicit declassify/sanitize
  decision; (b) historical commits contain the names — history rewrite
  requires specific user authorization.

### B. Acceptance and evidence findings

- **EV-01 (P1)** — RESOLVED. Gate 3 S5 now runs missingdep followed by a
  FRESH drawing with no pre-existing PDF: `[Failed, Success]` with the
  second PDF's header/page count verified via PdfGate (PASS 2026-08-30).
- **EV-02 (P1)** — RESOLVED. Gate 3 S10 writes the cancel flag while item 1
  is mid-plot (temp PDF observed); item 1 finishes atomically `Success`
  with a valid temp PDF, item 2 `Cancelled`, no item-2 temp/output. App
  publication boundary covered by the CR-03 unit test. One sequential NX
  session by construction. Residual race (~10 ms watcher) documented in the
  Gate 3 README.
- **EV-03 (P1)** — RESOLVED. `tools/NxDrawingPdfExporter.FixtureBuilder`
  (validation-only; saves only its own NEW part; run through
  `run_managed.exe`) creates a real PRT with one A4 sheet and zero drafting
  views. Gate 3 S11 proves `NoValidSheets` (no PDF, no temp) followed by a
  real successful export (PASS 2026-08-30).
- **EV-04 (P1)** — PARTIALLY RESOLVED, **user action required**. The
  inaccurate limitation claim is corrected (fixed-coordinate inner controls
  exist; risk stated honestly). The 100%/125%/150% DPI screenshots and
  visual inspection remain open and require the user to change display
  scaling (machine-config boundary). Refactoring, if QA exposes defects,
  happens after that evidence exists.
- **EV-05 (P1)** — OPEN, **user action required**. Static audit + offline
  build/publish remain supporting evidence only; the physically
  disconnected clean-extraction run must be performed by the user.
- **EV-06 (P2)** — FIXED. The false byte-equality claim is removed; the
  release README records measured facts for the rebuilt package
  (Success, 1 page, A2 MediaBox, 163,102 bytes, hashes unchanged) and the
  rendered page PNG exists in gitignored QA artifacts.
- **EV-07 (P3)** — PARTIALLY RESOLVED. README status is downgraded to
  "implementation candidate; acceptance pending"; final line-by-line
  reconciliation of plan checkboxes happens only after the second review
  passes and EV-04/EV-05 evidence exists.

## Verification evidence for the second review

- Test suites (all re-run after fixes): Contracts 19, Core 94, App 31 —
  0 failures. Release suite + build: 0 failures, 0 errors, with the known
  NU1702 Worker-test cross-target-framework compatibility warning (gated by
  `publish-portable.ps1`, which aborts on failure; run completed
  `PUBLISH OK`).
- Gate 3 full rerun: PASS, exit 0, 2026-08-30. Log (gitignored):
  `artifacts/gate-3-run-20260830-015258.log`; fixtures under
  `artifacts/gate-3-fixtures/<guid>/`.
- Release package rebuilt: 7 files; GUI 117,881,061 bytes.
  `tools/inspect-release.ps1` → `INSPECT OK` (zero developer-path hits;
  approved NX-root constant whitelisted). Manifest
  `artifacts/release/manifest.sha256` verified against the package AND a
  clean extraction outside the repo (7/7).
- Clean-extraction controlled export with the packaged worker: Success,
  valid PDF (1 page, 1683.78×1190.55 pt), 163,102 bytes, page rendered to
  PNG (`artifacts/release-qa/<guid>/rendered/page-01.png`), source hashes
  unchanged. A drawing-only fixture attempt correctly failed with
  "模型依赖未完全加载" (status 7), no PDF, no temp.
- `tools/test-inspect-release.ps1`: PASS (3/3 scenarios).
- `git status --short` (end of repair work): only the pre-existing untracked
  handoff/prompt docs and `.omo/`; tracked tree clean.

## Still open before acceptance

1. EV-04: user-operated GUI screenshots at 100/125/150% DPI + inspection.
2. EV-05: user-operated disconnected-network clean-extraction run.
3. User decisions on the design-spec sample names and (separately) on
   authorizing any history rewrite (CR-09 residual).
4. After the second review: plan-checkbox reconciliation and README
   status restoration (EV-07 tail).

## Addendum — second-round repairs (2026-08-30, after the second review)

The second review accepted the reproduced tests/build but listed 3 P1 and
2 P2 items ("暂不通过"). Resolution, all on commit `fb785ff` plus doc
updates:

1. **[P1] Worker-stage cancellation discarded the completed current file.**
   The worker already accounts for a cancel flag observed at a file
   boundary (`[Success, Cancelled]`), but the GUI publication loop then saw
   the still-present flag and deleted/`Cancelled` the completed item.
   Fix: the controller snapshots the flag state when the worker exits; only
   flags that appear AFTER worker exit cancel publication. The terminal
   progress text also distinguishes worker-side cancellation. Tests:
   `Cancel_DuringWorkerRun_PublishesCompletedFilesAndKeepsWorkerCancelledRest`
   (new FakeWorkerLauncher models the real state machine: per-item flag
   check, `Cancelled=true` on observation), and
   `Cancel_DuringRun_WritesCancellationFlag` now asserts final outcomes
   (all items the worker completed before observing the flag are published).
   End-to-end: Gate 3 **S12** drives the REAL `ApplicationController` with
   the REAL worker via a new `cancel` driver mode that calls
   `controller.Cancel()` while item 1 is mid-export; asserts
   `[Success, Cancelled]` with item 1 published and page-count-verified.
2. **[P1] Generic absolute build-path scan missing; upstream paths
   unnoticed.** The inspector now enforces an explicit documented policy
   (see `docs/verification/release/README.md`): any drive path carrying
   source/build indicators (`\src\`, `\obj\`, `.cs/.cpp/.pdb/...`) must
   match a reviewed upstream allowlist — the approved NX root constant,
   `D:\a\_work\` (Microsoft's official .NET runtime CI root, inherent to
   the official runtime pack) and `D:\repos\empira\` (PDFsharp upstream
   root, inherent to the official NuGet binary) — otherwise the gate fails.
   Binary noise without indicators is not treated as a path (the runtime
   binary contains random `X:\...`-like byte sequences that would make any
   "all drive paths are forbidden" policy permanently unenforceable).
   Self-test grew to 7 scenarios (upstream allowed / unknown source path
   rejected / user-profile path rejected / noise not misreported) and the
   real package re-passed INSPECT OK with zero unaccounted paths.
3. **[P1] Per-scenario source-hash reports.** Every Gate 3 scenario now
   records SHA-256 of every `.prt` it actually opens (drawing AND model
   copies) immediately before/after the run via `Invoke-ProtectedScenario`;
   the structured report (`source-hash-report.json`, gitignored) for the
   final run holds 35 entries across S1–S12 with zero mismatches, and any
   change is a hard failure.
4. **[P2] Untrusted-result paths left run temp PDFs.** Invalid
   `result.json`, RunId mismatch, and non-Success worker results now clean
   run-owned temp files. Tests:
   `Run_InvalidResultJson_CleansRunOwnedTemps`,
   `Run_ResultFromDifferentRunId_AllItemsFailWithoutPublication`
   (strengthened), `Run_NonSuccessWorkerResult_CleansStrayRunOwnedTemp`
   (fake worker that reports Failed but leaves a stray temp).
5. **[P2] Real fault injection for CR-04.** New `Worker.Tests` project with
   two tests running the REAL `Program.Main --run-job` → `NxBatchRunner` →
   `BatchStateMachine` → `JobJsonSerializer` path through new test-only
   seams (`NxBatchRunner.ProcessorOverride` / `SnapshotWriterOverride`,
   production never sets them): (a) first snapshot durably written, second
   snapshot write throws → exit 22, final result.json retains item 1,
   unresolved items Failed with the sanitized fatal message, `FatalError`
   non-empty single-line; (b) merge write fails against a genuinely locked
   result.json (ReadShare lock: reads pass, writes fail) → exit 21 and the
   durable snapshot remains the valid previous JSON.

Verification for the addendum:

- Full Release suite during the package rebuild: Contracts 19 + Core 94 +
  Worker 2 + App 34 = **149 passed, 0 failed**; Release build 0 errors with
  the known NU1702 Worker-test compatibility warning.
- Gate 3 rerun with S12 and per-scenario hash reports: **PASS, exit 0**
  (`artifacts/gate-3-run-r3.log`; an earlier r2 attempt passed S1–S11 but
  aborted at S12 on a PowerShell 5.1 native-stderr quirk — the driver was
  fixed to keep stdout/stderr pure, then the full matrix passed).
- Package rebuilt with the fixed binaries (GUI 117,881,061 bytes);
  `inspect-release.ps1` INSPECT OK (zero unaccounted paths); manifest
  verified 7/7 against the package AND a fresh extraction outside the
  repo; controlled export with the packaged worker: Success, 1 page A2,
  163,101 bytes, rendered page PNG, source hashes unchanged.
- Inspector self-test: 7/7 PASS.

Still open before acceptance (unchanged): EV-04 user DPI screenshots,
EV-05 user disconnected run, design-spec names + history decision, final
checkbox reconciliation.

## Release decision addendum (2026-08-30)

The user subsequently authorized v1.0.0 release with EV-04 and EV-05 kept as
explicit known limitations rather than claimed as completed evidence. EV-04
is tracked by GitHub Issue #2 and EV-05 by Issue #3 for a future major-version
work cycle. The approved design sample identifiers and all reachable `main`
and feature-branch history were sanitized before the repository can ever be
made public. Final checklist reconciliation records the two deferred items as
unchecked with their issue references; all implemented and evidenced steps
are checked.
