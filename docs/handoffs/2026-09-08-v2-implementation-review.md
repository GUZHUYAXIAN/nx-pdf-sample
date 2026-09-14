# V2 implementation review handoff

Updated 2026-09-14. Candidate only. User permits proceeding without waiting for
the four manual acceptance activities; disposition USER-WAIVED / UNVERIFIED,
not independently observed PASS. Sole implementer retained; reviewer was
read-only and did not execute GUI, NX, tests, builds, edits or Git mutations.

Later authorization on 2026-09-14: local commits, GitHub push and open-source
publication are now explicitly requested. Earlier approval-gated statements below
are historical. License choice and privacy audit precede public visibility. No new
tag, Release, binary upload or acceptance-Issue mutation is requested. The technical
UNVERIFIED acceptance status remains unchanged.

## Range and file inventory

Base/HEAD both `414dc5fd6f43cf2f8fa6b97812b607405221967d`, detached worktree `2d02`.
Review `git diff HEAD` AND the following untracked V2 files; an equal-SHA diff is empty.
No staged content. Do not stage unrelated `.dotnet-home/`, `.nuget/`, `.omo/` or
August handoffs/prompts. No private samples were accessed by this continuation.

Tracked V2 delta:

```text
Directory.Build.props
README.md
docs/verification/release/known-limitations.md
src/NxDrawingPdfExporter.App/ApplicationController.cs
src/NxDrawingPdfExporter.App/MainForm.cs
src/NxDrawingPdfExporter.App/MainForm.Designer.cs
src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj
src/NxDrawingPdfExporter.App/Runtime/NxInstallationDetector.cs (deleted)
tests/NxDrawingPdfExporter.App.Tests/ApplicationControllerTests.cs
tests/NxDrawingPdfExporter.App.Tests/NxInstallationDetectorTests.cs
tools/inspect-release.ps1
tools/publish-portable.ps1
tools/test-inspect-release.ps1
```

Untracked product/test V2 additions:

```text
src/NxDrawingPdfExporter.App/Configuration/INxSettingsStore.cs
src/NxDrawingPdfExporter.App/Configuration/JsonNxSettingsStore.cs
src/NxDrawingPdfExporter.App/Configuration/NxSettings.cs
src/NxDrawingPdfExporter.App/Logging/NxDetectionLog.cs
src/NxDrawingPdfExporter.App/Runtime/NxInstallationModels.cs
src/NxDrawingPdfExporter.App/Runtime/NxInstallationValidator.cs
src/NxDrawingPdfExporter.App/Runtime/Detection/EnvironmentNxCandidateSource.cs
src/NxDrawingPdfExporter.App/Runtime/Detection/INxInstallationCandidateSource.cs
src/NxDrawingPdfExporter.App/Runtime/Detection/InstalledApplicationNxCandidateSource.cs
src/NxDrawingPdfExporter.App/Runtime/Detection/NxInstallationDiscoveryService.cs
src/NxDrawingPdfExporter.App/Runtime/Detection/RegistryNxCandidateSource.cs
tests/NxDrawingPdfExporter.App.Tests/ApplicationControllerNxTests.cs
tests/NxDrawingPdfExporter.App.Tests/MainFormStateTests.cs
tests/NxDrawingPdfExporter.App.Tests/NxInstallationCandidateSourceTests.cs
tests/NxDrawingPdfExporter.App.Tests/NxInstallationDiscoveryServiceTests.cs
tests/NxDrawingPdfExporter.App.Tests/NxSettingsStoreTests.cs
```

V2 document additions: the 2026-09-08 V2 design/implementation plan; colleague and
Terra prompts under docs/prompts; this handoff; docs/verification/v2/{README.md,
candidate-status.md,build-blocker.md,local-manual-checklist.md,colleague-manual-checklist.md}.
The historical candidate-status.md and build-blocker.md retain earlier evidence.

## Review findings and disposition

1. Important: selection changed state then awaited persistence without busy lock.
   Fixed by busy/try/finally around both selection methods. Two controlled delayed-save
   rows now reject overlapping refresh/select/start and expose disabled UI state.
2. Important: quoted UGII_ROOT_DIR was tested for terminal UGII before stripping quotes.
   Fixed normalization order in EnvironmentNxCandidateSource; no broad path search added.
3. Important: source failures lacked Source and controller discarded diagnostics.
   Added stable source identity and separate lazy local NX diagnostic sink; only code,
   source and exception type are written, not exception messages or install/sample paths.
   Write failure retains valid in-memory selection and displays a warning.

Independent re-review inspected all three fixes and new tests, reported no remaining
concrete Important issue, and accepted the code subject to full automated/package
verification. That verification subsequently passed. This does not approve a merge
or replace final human evidence. The subsequent r2 continuation resolved the minor
multiple-candidate discrepancy in favor of approved design 7.1; the same read-only
reviewer accepted the visibility, retained highlight, fallback and formatting wiring.

## RED → GREEN (commands from isolated worktree)

Use existing dotnet with `test tests/NxDrawingPdfExporter.App.Tests -c Release
--no-restore --filter <filter>`:

| Filter | RED before implementation | GREEN |
|---|---|---|
| `FullyQualifiedName~Selection_SavePending\|FullyQualifiedName~EnvironmentSource_QuotedUgiiRoot` | 3 failed: busy false twice; wrong parent path once | 3 passed |
| `FullyQualifiedName~SourceFailure_IdentifiesSource\|FullyQualifiedName~Detection_Issues_AreLogged` | 5 failed: source null three times, absent log twice | 5 passed |
| `FullyQualifiedName~DetectionLog_LazilyCreatesPersistentLocalLog` | CS0246: new NxDetectionLog missing | 1 passed; combined with log-write failure characterization 2 passed |

The log-write-failure test is supplementary coverage of code already added in the
same repair, not a separately observed RED. No historical RED for Tasks 1–2 is claimed.
Earlier continuation: 7 failed/11 passed focused controller/settings, then App67/full182;
see historical candidate-status.md. This turn's baseline182 had no test failure.

### r2 UI continuation

Baseline full suite: 192/192, no failures. Changed product file: MainForm.cs only;
test file: MainFormStateTests.cs. Plan/UI documents were aligned with design 7.1.

- Filter `FullyQualifiedName~BuildNxViewState_ChosenFromMultiple`: RED two rows
  (ShowCandidateList false after explicit/saved selection), then GREEN two rows.
- Filter `FullyQualifiedName~ChooseNxCandidate|FullyQualifiedName~FormatNxCandidate`:
  RED CS0117 because pure UI helpers did not exist. After implementation,
  `FullyQualifiedName~MainFormStateTests` passed 9/9 (including prior test).
- New cases retain a case-insensitive highlighted candidate from the new list,
  fall back to the current installation/first item, drop stale highlight on empty
  discovery and display root+version. These are state tests, not GUI screenshots.
- Real ComboBox remains bound to NxInstallation; no auto-selection command runs
  merely because formatting or highlight changes. Final code re-review passed.

## Verification / next action

Full Release200: App85, Core94, Contracts19, Worker2; zero failures/skips. Full build
zero errors, one unchanged NU1702 (net10 test reference to net48 Worker). Static
policy and inspector self-tests PASS, package INSPECT OK, clean-extraction manifest
seven matches. Current ZIP and exact commands are in ../verification/v2/README.md.
The tracked diff has 13 files; full current sizes are available with `git diff --stat`.
This excludes untracked additions listed above. All changes remain reviewable, unstaged.

Live NX, launcher process evidence, source before/after hashes, PDF rendering,
100/125/150% screenshots, physical offline, colleague path: no evidence reviewed.
The user's 2026-09-14 direction removes the workflow wait, not this evidence gap.
No further request for those materials is needed to prepare this qualified handoff.
On 2026-09-14 the existing r2 ZIP hash and seven extracted-file hashes were rechecked
and matched. Product code/payload were unchanged; no tests/builds were rerun that day.
Do not create PASS evidence files, change remote Issues, or claim original full V2
verification. No staging/commit/push/tag/Release authorized; ask separately before
the next local commit step. Historical RED gaps remain documented rather than invented.
