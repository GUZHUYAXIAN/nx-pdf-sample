# V2 candidate checkpoint — 2026-09-10

Historical checkpoint. The replacement candidate and current 2026-09-13 status
are recorded in `README.md` in this directory. Keep this earlier ZIP unchanged,
but do not use it for final acceptance after the review fixes.

Not V2 complete. Task 8 awaits user-operated live evidence; Tasks 9–10 are
NOT RUN and Task 11 independent review/reconciliation is incomplete.
Successful live evidence README is intentionally not created yet.

## Automated evidence

Worktree: isolated `2d02/nx-pdf-sample`, detached base
`414dc5fd6f43cf2f8fa6b97812b607405221967d`. No new commit/staging.
Inherited dirty/untracked files remain untouched unless specifically part of V2.

- Full Release test: `dotnet test NxDrawingPdfExporter.slnx -c Release --no-restore`:
  182 passed (Core 94, Contracts 19, Worker 2, App 67), 0 failed/skipped.
- Release build: 0 errors; known NU1702 warning is the net10 Worker test project's
  reference to the net48 Worker, not a PDFsharp warning.
- Authorized empty-source offline restore succeeded; see `build-blocker.md`.
  All subsequent build/test/publish used `--no-restore`.
- Portable publication succeeded, seven-file package inspected successfully.
- Publish policy self-test passed. Inspector self-tests previously passed;
  deliberately rejected fixture packages are expected failures, not product failures.
- ZIP was extracted to a new ignored directory, then `inspect-release.ps1
  -VerifyManifestOnly` with explicit extraction/manifest paths reported seven matches.
- `git diff --check` passed (line-ending conversion warnings only).

## This continuation's RED → GREEN

Focused command: `dotnet test tests/NxDrawingPdfExporter.App.Tests -c Release
--no-restore --filter "FullyQualifiedName~ApplicationControllerNxTests|FullyQualifiedName~NxSettingsStoreTests"`.
RED: 7 failed, 11 passed. New assertions exposed missing successful-selection
persistence/root display, overlapping detection/run acceptance, generic manual
failure text, stale selected-installation execution, and three malformed JSON shapes.
After minimal implementation changes, all App tests passed (67), then full suite 182.
Historical Task 1–2 RED outputs are unavailable and are not reconstructed or claimed.

Files for these repairs: `ApplicationController.cs`, `Configuration/JsonNxSettingsStore.cs`,
`ApplicationControllerNxTests.cs`, `NxSettingsStoreTests.cs`.
Layout follow-up: `MainForm.cs`, `MainForm.Designer.cs`; parent Tab ordering,
horizontal layout and disabled input states require manual visual verification.
Packaging follow-up: `tools/publish-portable.ps1`; explicit existing cache,
fresh release destination, and process-local first-run safety switches.

## Frozen artifact

Ignored artifact: `artifacts/release/NXDrawingPdfExporter-v2-candidate.zip`.
SHA-256: `4ABE08D675AEF81E87523E799C46B234BC440D6C01027588CE27D8DE22FC8101`.
Adjacent `manifest.sha256` identifies the seven payload files;
`candidate.sha256` identifies the ZIP. Do not rebuild between local and colleague QA.
No private samples, source hashes, Siemens assemblies or generated PDFs are packaged.

## Open gates and next action

Use `local-manual-checklist.md`, sections 1–3, for Task 8 first. No live NX,
source-integrity, PDF visual, DPI or physical-offline acceptance is asserted.
Only after local evidence passes should DPI/offline and colleague validation proceed.
No commit, push, tag, Release or Issue changes are authorized by this checkpoint.

An SDK first-run message claimed development-certificate installation during the
first packaging invocation. Read-only CurrentUser/My inspection found only the
existing certificate dated 2026-08-15, not a newly dated certificate. Nothing was
deleted, trusted or exported. The script now preserves existing CLI_HOME and
disables first-run certificate generation and telemetry for its own process.
Those final script-only safety changes were policy-tested, not republished;
they do not alter the frozen product payload.
