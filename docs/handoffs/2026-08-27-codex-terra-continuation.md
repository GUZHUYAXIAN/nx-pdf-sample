# Codex Terra continuation handoff

- Date: 2026-08-27
- Repository: `E:\Codex\projects\nx-pdf-sample`
- Required execution environment: local saved project, directly in the existing checkout
- Current branch: `feature/nx-drawing-pdf-exporter-v1`
- Reviewed HEAD before this handoff commit: `960a546`
- Approved specification: `docs/superpowers/specs/2026-08-26-nx-drawing-pdf-exporter-design.md`
- Implementation plan: `docs/superpowers/plans/2026-08-26-nx-drawing-pdf-exporter-implementation.md`

## Verified progress

The implementation is partially complete and should be continued, not restarted.

1. Task 1 solution/toolchain scaffolding is committed.
2. Task 2 versioned Job/Result protocol is committed.
3. Task 3 Gate 1 is committed and has real-machine evidence:
   - `run_managed.exe` loaded the `net48` x64 probe;
   - real `NXOpen.Session` and `UFSession` were both obtained;
   - exit code was 0;
   - no Siemens DLL is tracked or copied into build output.
4. A fresh review run on 2026-08-27 passed all 11 committed Contracts tests.
5. The current full solution is intentionally RED because Task 4 production types have not been implemented.

## Current uncommitted state — preserve it

Do not discard, reset, clean, stash away, or overwrite the following:

- `tests/NxDrawingPdfExporter.Core.Tests/InputDiscoveryServiceTests.cs`
- `tests/NxDrawingPdfExporter.Core.Tests/OutputPlannerTests.cs`
- `.omo/` tool-state directory

The two test files are the beginning of Task 4 and must be treated as previous developer work. The tracked working tree is otherwise clean.

The repository root also contains an ignored PRT copy outside `samples/private/`. It is not the authoritative validation sample, has different file facts from the private original, and must not be used, moved, renamed, deleted, committed, or “cleaned up” during implementation. Only `samples/private/` contains the authoritative private sample set, and its files must remain untouched.

## Review findings to address before continuing Task 4

1. `InputDiscoveryServiceTests.cs` is missing `using System.Text.RegularExpressions;`. Add it so the RED state is caused only by missing production behavior.
2. The fake filesystem's `GetFullPath` currently trims a trailing slash but does not collapse `..`, while one test expects canonical-path deduplication. Correct the fake to model `GetFullPath` accurately; do not bypass the filesystem abstraction in production merely to satisfy a faulty fake.
3. Add protocol regression tests before changing protocol code:
   - `JobJsonSerializer.Deserialize<JobRequest>("null")` must throw `ProtocolException`, never return null;
   - `OutputMode.UnifiedDirectory` must require a nonblank absolute `UnifiedOutputDirectory`.
   Implement the minimal protocol fixes and commit them separately before the Task 4 production implementation.
4. Reconcile plan checkboxes from evidence. Task 1 steps 3–5 and Task 3 step 4 remain unchecked even though the corresponding package policy/project graph and launcher-syntax evidence exist. Verify first, then update the boxes; do not mark anything from assumption.

## Current validation facts

Run with the project-local caches and read-only SDK:

```powershell
$taskRoot = 'E:\Codex\projects\nx-pdf-sample'
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $taskRoot '.tools\nuget-http-cache'
Set-Location -LiteralPath $taskRoot
```

Observed on 2026-08-27:

```text
Contracts tests: 11 passed, 0 failed
Full solution: failed during Core.Tests compilation
Expected missing production namespaces: Core.Input and Core.Output
Additional test-only compile issue: missing Regex using
Forbidden tracked NXOpen DLL / PRT / PDF / private sample files: none
```

Required NuGet packages, including PDFsharp 6.2.4, MSTest 4.0.2, `NETStandard.Library`, and the .NET Framework reference assemblies, are already present under the repository-local `.tools/nuget-packages`. Use `--no-restore` first. Do not download, update, replace, or globally install dependencies.

## Continuation order

1. Read `AGENTS.md`, README, complete specification, complete implementation plan, and this handoff.
2. Confirm the exact branch and uncommitted files above. If the task is running in a Codex worktree or a different checkout, stop and ask the user to reopen it in the saved project directly.
3. Fix the two test-harness issues and add/fix the two protocol regressions using TDD; commit the protocol hardening separately.
4. Resume Task 4 from the preserved uncommitted tests, get the focused tests GREEN, run the relevant suite, update verified checkboxes, and commit Task 4.
5. Continue Tasks 5–12 sequentially. Do not stop after Task 4 while safe, in-scope work remains.
6. Respect all three live NX gates. Never replace real evidence with mocks.
7. Leave the feature branch locally reviewable; do not push or create a release.

## Completion report

Use the plan's “Final Acceptance Evidence” section verbatim as the completion checklist. Report exact commands, test counts, NX evidence, PDF evidence, source before/after hashes held only in ignored artifacts, GUI DPI screenshots, package inspection, known limitations, commit list, and final `git status`.
