# Codex GPT-5.6 Terra continuation prompt

Copy everything between the horizontal rules into the new Codex task.

---

Continue the existing NX Drawing PDF Exporter implementation directly in this saved local project:

`E:\Codex\projects\nx-pdf-sample`

This is a continuation, not a restart. Work autonomously through the remaining approved plan while preserving existing code and uncommitted tests.

First, verify that your current working directory resolves exactly to `E:\Codex\projects\nx-pdf-sample`, the branch is `feature/nx-drawing-pdf-exporter-v1`, and the existing untracked Task 4 tests are visible. You must run in the saved project/local checkout, not a Codex worktree or another clone. If the path, branch, or untracked tests do not match, stop immediately and tell me to reopen the task in the saved project directly; do not copy files between checkouts and do not create another branch.

Before editing, completely read these files in order:

1. `AGENTS.md`
2. `README.md`
3. `docs/superpowers/specs/2026-08-26-nx-drawing-pdf-exporter-design.md`
4. `docs/superpowers/plans/2026-08-26-nx-drawing-pdf-exporter-implementation.md`
5. `docs/handoffs/2026-08-27-codex-terra-continuation.md`

Use `superpowers:executing-plans` to execute the approved plan, `superpowers:test-driven-development` for every feature/fix, `superpowers:systematic-debugging` for unexpected failures, and `superpowers:verification-before-completion` before any completion claim. Do the work yourself in this task; do not create a new Codex task, worktree, repository, or branch.

Current verified baseline:

- Tasks 1–3 are substantially implemented and committed.
- Gate 1 is a real PASS: NX 10 `run_managed.exe` loaded the `net48` x64 probe, and real Session plus UFSession were obtained with exit code 0.
- The 11 Contracts tests pass in a fresh run.
- No Siemens DLL, PRT, PDF, artifacts, or private sample file is tracked.
- Task 4 has started with two untracked test files. Preserve them.
- The full solution is currently RED because `Core.Input` and `Core.Output` production types do not exist yet; this is expected at the Task 4 TDD boundary.

Before implementing Task 4, perform the narrowly scoped review corrections documented in the handoff:

1. Add the missing Regex namespace import to `InputDiscoveryServiceTests.cs`.
2. Make its fake filesystem canonicalize `..` consistently with the `IFileSystem.GetFullPath` contract; do not bypass the production abstraction to compensate for the fake.
3. Add failing regression tests proving that a JSON root of `null` throws `ProtocolException` and that unified-output mode rejects a missing/blank/nonabsolute unified directory; then implement the minimal protocol fixes and commit them separately.
4. Verify and reconcile the few stale plan checkboxes using existing files and Gate 1 evidence. Never mark a step complete merely because this prompt says so.

Then resume Task 4 from the existing untracked tests. First obtain a clean RED caused only by missing intended production behavior, implement the smallest Core discovery/output services, run focused tests and the relevant solution tests, update only evidence-backed checkboxes, and commit. Continue Tasks 5 through 12 sequentially without waiting for another prompt while safe in-scope work remains.

Development environment:

```powershell
$taskRoot = 'E:\Codex\projects\nx-pdf-sample'
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $taskRoot '.tools\nuget-http-cache'
Set-Location -LiteralPath $taskRoot
```

All reviewed packages are already in the repository-local cache. Prefer `--no-restore`. Do not download, install, upgrade, or replace any SDK, NuGet package, renderer, or system component. Do not modify the sibling `nx-step-launcher`; it is read-only.

Preserve all existing user/developer work. Do not run reset, clean, checkout-discard, broad formatting, or deletion commands. The untracked `.omo/` directory is tool state; leave it alone. An ignored PRT copy also exists in the repository root outside `samples/private/`; it is not the authoritative sample. Do not use, move, rename, delete, commit, or clean it. Never expose private sample names, hashes, drawings, or metadata in tracked evidence.

The hard product boundaries in `AGENTS.md` and the specification remain controlling: exact NX 10.0.0.24 only; GUI and Worker stay separate; GUI never loads NXOpen; Worker uses `run_managed.exe`; no Siemens redistribution; no NX save calls; one serial NX session; actual Drafting View is the only positive page rule; missing dependencies are failures, not blank pages; navigator order is proved on the real sample; safe overwrite retains the old PDF until managed validation succeeds; cancellation stops before the next file; runtime is offline.

For live NX work, compute source and associated-model hashes immediately before and after each run and keep values only in ignored artifacts. Use destructive/failure-path scenarios only on temporary copies under ignored `artifacts/`. Any source hash change, Gate failure, need for a new download/install, specification conflict, destructive action, push, release, or scope expansion is a stop condition requiring my decision. Ordinary code changes, tests, local commits, and non-destructive verification inside the approved plan are authorized; do not repeatedly ask for permission.

After each plan task, run its focused verification, make a small local commit, and continue. If the context becomes constrained, leave the current task at a coherent tested commit, update only verified plan checkboxes, and write a concise continuation note before stopping.

Do not claim V1 complete until every item in the plan's Final Acceptance Evidence is genuinely present. Final delivery is the local feature branch only: no push and no release. Lead the final report with whether the product is actually complete, then give exact test/build/NX/PDF/GUI/package evidence, commit list, known limitations, and final `git status`.

Start now by reporting a concise baseline audit (maximum 12 lines), then immediately perform the protocol regression RED tests and continue execution unless a defined stop condition is met.

---
