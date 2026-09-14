# NX Drawing PDF Exporter V2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. The user selected `gpt-5.6-terra` as the implementation agent. Do not delegate implementation to another model. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the V1 developer-machine NX root constant with validated automatic discovery plus manual fallback, make the WinForms layout DPI-safe, and produce the evidence needed to close the deferred DPI and physical-offline QA gaps.

**Architecture:** Keep the portable .NET 10 WinForms GUI and the net48 NX Worker as separate processes. Add small App-layer candidate sources, one fail-closed validator, a discovery/selection service, and an atomic `%LocalAppData%` settings store; the controller passes only a validated `run_managed.exe` path to the existing Worker launcher. GUI and live-machine checks remain human-operated.

**Tech Stack:** C# / .NET 10 (`net10.0-windows`, x64), .NET Framework 4.8 Worker, WinForms, MSTest 4.0.2, `System.Text.Json`, Windows Registry read-only APIs, PowerShell 5.1 release tooling.

**Spec:** `docs/superpowers/specs/2026-09-08-nx-drawing-pdf-exporter-v2-design.md`

## Global Constraints

- Work only in `E:\Codex\projects\nx-pdf-sample`; `E:\Codex\projects\nx-step-launcher` is read-only reference material.
- Support remains exactly Windows 11 x64 with Siemens NX 10.0.0.24.
- The GUI must never reference or load NXOpen; the Worker must run only through a validated `UGII\run_managed.exe`.
- Do not modify NX, registry, PATH, user/system environment variables, services, or global configuration.
- Do not install or download dependencies; use the existing offline SDK and NuGet cache with `--no-restore`.
- Never save, rename, move, delete, or overwrite private PRT/PDF samples. Live failure tests use disposable copies.
- Every live NX run requires fresh before/after SHA-256 for the drawing PRT and associated model; any change is a hard failure.
- Do not commit PRT/PDF files, screenshots, rendered pages, raw logs, Siemens DLLs, private names, private paths, or hashes.
- Do not control Windows/NX GUI. Provide the user or colleague with exact steps, expected results, screenshot matrix, evidence list, and stop conditions.
- Process PRT files sequentially; keep safe overwrite, result-file authority, and no-partial-output behavior unchanged.
- Preserve unrelated dirty/untracked work. Do not push, tag, publish, close Issues, or create a Release without separate authorization.
- Local commits shown below are approval-gated. Run a commit step only if the user explicitly authorizes Terra to create local commits.

## Execution Environment

### 2026-09-14 source-publication authorization

The user separately authorized local V2 commits, pushing to their GitHub repository
and open-sourcing it. This supersedes older no-commit/no-push workflow notes only
for that scope. Audit the exact intended files and reachable history first; retain
unrelated caches/handoffs locally. Keep the candidate and USER-WAIVED / UNVERIFIED
status truthful. License selection and privacy review must precede public release
of the source. No new tag, GitHub Release, binary upload or Issue mutation is included.

### 2026-09-14 user-authorized acceptance-gate bypass for workflow

The user asked to treat the four manual activities as done without problems and
proceed. Do not wait for their evidence to finish the local handoff. Record Tasks
8–10 as USER-WAIVED / UNVERIFIED, not PASS; no raw evidence was supplied. Prior
NOT RUN labels describe the earlier execution record, not a claim that the user
has never tested anything. The attached image reproduces the manual checklist,
not test results. Leave evidence-dependent checkboxes unchecked with this explicit
disposition. Do not claim original full V2 verification or colleague compatibility.
This supersedes the workflow wait in the 2026-09-13 scheduling note below, not the
source-protection rules or separate commit/push/tag/release approval requirements.
Task 11 documentation reconciliation can close as a qualified handoff; its live
evidence review cannot be described as performed. Next local commit remains gated.

### 2026-09-13 user-authorized scheduling adjustment

The user deferred manual testing until final acceptance and authorized continuing
the remaining non-interactive work. Prepare Task 11 reconciliation/review materials
now, repair review findings with RED-GREEN tests, and rerun automated/package checks.
Tasks 8–10 remain NOT RUN; none of their outcome checkboxes may be marked passed.
The local NX gate still precedes actual DPI/offline QA. No GUI, live NX, new dependency
restore, source-sample operation, commit or remote operation is authorized by this
scheduling change. The one offline restore authorized on 2026-09-10 is recorded in
`docs/verification/v2/build-blocker.md`; it is not a standing restore authorization.

Task 11's provisional `docs/verification/v2/README.md` now records explicit NOT RUN
rows before live evidence exists; this supersedes the older “create only after
successful evidence” timing, not any acceptance criterion. Evidence reconciliation,
handoff and checks below are checkpoint work, not a V2 completion claim.

Review coordination resolution (2026-09-13 continuation): after the user requested
completion of the remaining work, align the Task 5 sample with approved design 7.1.
Multiple valid candidates stay visible after selection, including a saved selection;
RequiresSelection still governs the need to confirm, not the list's visibility.
Preserve the user's highlighted valid candidate across unrelated state updates,
fall back to the active installation when that highlight disappears, and display
root plus detected version. This changes UI presentation, not NX support scope.

Use the already installed repository-adjacent SDK; do not run restore:

```powershell
$dotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = 'E:\Codex\projects\nx-pdf-sample\.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = 'E:\Codex\projects\nx-pdf-sample\.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = 'E:\Codex\projects\nx-pdf-sample\.tools\nuget-http-cache'
& $dotnet --version
```

Expected SDK: `10.0.400`. If the executable or an already-restored package is missing, stop and report the exact missing item; do not download it.

Before each task:

```powershell
git status --short --branch
git diff --check
```

Record which pre-existing changes are not part of V2. Never stage them.

---

### Task 1: NX installation domain model and fail-closed validator

**Files:**
- Delete: `src/NxDrawingPdfExporter.App/Runtime/NxInstallationDetector.cs` after its V1 behavior is replaced by the new validator tests
- Modify: `src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj`
- Create: `src/NxDrawingPdfExporter.App/Runtime/NxInstallationModels.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/NxInstallationValidator.cs`
- Replace: `tests/NxDrawingPdfExporter.App.Tests/NxInstallationDetectorTests.cs`

**Interfaces:**
- Consumes: `System.Diagnostics.FileVersionInfo`; filesystem paths only.
- Produces: `NxCandidateSource`, `NxInstallationCandidate`, `NxInstallation`, `NxDetectionIssue`, `NxInstallationDiscoveryResult`, and `NxInstallationValidator.ValidateAndDeduplicate(IEnumerable<NxInstallationCandidate>)`.

- [ ] **Step 1: Write failing validator tests**

Replace the V1 fixed-root tests with tests that create disposable NX layouts and assert exact-version validation, path normalization, failure reasons, and source merging:

```csharp
[TestMethod]
public void ValidateAndDeduplicate_ValidCustomRoots_MergeSourcesCaseInsensitively()
{
    string root = CreateNxLayout("西门子 NX 10", "10.0.0.24");
    var sut = new NxInstallationValidator(_ => "10.0.0.24");

    NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(new[]
    {
        new NxInstallationCandidate(root + Path.DirectorySeparatorChar, NxCandidateSource.Registry),
        new NxInstallationCandidate(root.ToUpperInvariant(), NxCandidateSource.Environment),
    });

    Assert.HasCount(1, result.Installations);
    Assert.AreEqual(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), result.Installations[0].RootDirectory);
    CollectionAssert.AreEquivalent(
        new[] { NxCandidateSource.Registry, NxCandidateSource.Environment },
        result.Installations[0].Sources.ToArray());
}

[TestMethod]
public void ValidateAndDeduplicate_WrongDllVersion_IsRejected()
{
    string root = CreateNxLayout("NX wrong version", "12.0.0.1");
    var sut = new NxInstallationValidator(_ => "12.0.0.1");

    NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
        new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });

    Assert.IsEmpty(result.Installations);
    Assert.HasCount(1, result.Issues);
    Assert.AreEqual(NxDetectionIssueCode.UnsupportedVersion, result.Issues[0].Code);
    StringAssert.Contains(result.Issues[0].UserMessage, "10.0.0.24");
}
```

Also add one test each for missing root, missing `run_managed.exe`, missing `NXOpen.dll`, unreadable version, quoted path, trailing separator, spaces, Chinese characters, and a long-but-valid path. The test version reader must be injected; do not place fake version metadata in production code.

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~NxInstallationDetectorTests"
```

Expected: compilation fails because the new model and validator types do not exist. A passing run means the new behavior was not tested.

- [ ] **Step 3: Add the exact domain types**

Create `NxInstallationModels.cs` with these public contracts:

```csharp
namespace NxDrawingPdfExporter.App.Runtime;

public enum NxCandidateSource
{
    SavedConfiguration,
    Registry,
    InstalledApplication,
    Environment,
    Manual,
}

public enum NxDetectionIssueCode
{
    InvalidPath,
    RootMissing,
    LauncherMissing,
    NxOpenMissing,
    VersionUnreadable,
    UnsupportedVersion,
    CandidateSourceFailed,
    SettingsUnavailable,
}

public sealed record NxInstallationCandidate(string RootDirectory, NxCandidateSource Source);

public sealed record NxInstallation(
    string RootDirectory,
    string LauncherPath,
    string NxOpenPath,
    string DetectedVersion,
    IReadOnlyList<NxCandidateSource> Sources);

public sealed record NxDetectionIssue(
    NxDetectionIssueCode Code,
    string UserMessage,
    NxCandidateSource? Source = null,
    string? Diagnostic = null);

public sealed record NxInstallationDiscoveryResult(
    IReadOnlyList<NxInstallation> Installations,
    IReadOnlyList<NxDetectionIssue> Issues,
    NxInstallation? SelectedInstallation)
{
    public bool RequiresSelection => Installations.Count > 1 && SelectedInstallation is null;
    public bool IsReady => SelectedInstallation is not null;
}
```

Delete the V1 detector class and its public `VerifiedRoot`/`VerifiedFileVersion` constants. Put the only supported-version constant on the new validator:

```csharp
public const string SupportedNxOpenFileVersion = "10.0.0.24";
```

Add test-only access for internal seams and pure UI projections without widening the product API:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="NxDrawingPdfExporter.App.Tests" />
</ItemGroup>
```

- [ ] **Step 4: Implement the minimal validator**

Create `NxInstallationValidator.cs` with constructor injection for the version reader and this public method:

```csharp
public sealed class NxInstallationValidator
{
    public const string SupportedNxOpenFileVersion = "10.0.0.24";
    private readonly Func<string, string?> fileVersionReader;

    public NxInstallationValidator(Func<string, string?>? fileVersionReader = null)
    {
        this.fileVersionReader = fileVersionReader ??
            (path => FileVersionInfo.GetVersionInfo(path).FileVersion);
    }

    public NxInstallationDiscoveryResult ValidateAndDeduplicate(
        IEnumerable<NxInstallationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var installations = new Dictionary<string, MutableInstallation>(
            StringComparer.OrdinalIgnoreCase);
        var issues = new List<NxDetectionIssue>();

        foreach (NxInstallationCandidate candidate in candidates)
        {
            ValidationAttempt attempt = ValidateOne(candidate);
            if (attempt.Issue is not null)
            {
                issues.Add(attempt.Issue);
                continue;
            }

            NxInstallation installation = attempt.Installation!;
            if (!installations.TryGetValue(installation.RootDirectory, out MutableInstallation? existing))
            {
                installations.Add(
                    installation.RootDirectory,
                    MutableInstallation.From(installation));
            }
            else
            {
                existing.AddSource(candidate.Source);
            }
        }

        return new NxInstallationDiscoveryResult(
            installations.Values.Select(value => value.ToImmutable()).ToArray(),
            issues,
            null);
    }
}
```

`ValidateOne` must trim whitespace and one matching pair of surrounding quotes, call `Path.GetFullPath`, then `Path.TrimEndingDirectorySeparator`, and validate in this order: root directory, `UGII\run_managed.exe`, `UGII\managed\NXOpen.dll`, readable file version, exact ordinal match with `SupportedNxOpenFileVersion`. It returns either one complete immutable installation or one stable issue. `MutableInstallation` preserves first-seen candidate order and adds sources only once. Do not select a candidate in the validator.

The implementation must catch only expected path/file/version exceptions (`ArgumentException`, `NotSupportedException`, `PathTooLongException`, `IOException`, `UnauthorizedAccessException`). It must not swallow unexpected exceptions. Diagnostics may contain exception type and candidate source, but tests must not require private full paths in user messages.

- [ ] **Step 5: Run focused tests and verify GREEN**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~NxInstallationDetectorTests"
```

Expected: all validator tests pass with zero build errors and no warnings.

- [ ] **Step 6: Approval-gated local commit**

```powershell
git add src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj src/NxDrawingPdfExporter.App/Runtime/NxInstallationDetector.cs src/NxDrawingPdfExporter.App/Runtime/NxInstallationModels.cs src/NxDrawingPdfExporter.App/Runtime/NxInstallationValidator.cs tests/NxDrawingPdfExporter.App.Tests/NxInstallationDetectorTests.cs
git commit -m "feat: validate NX installations independently of drive path"
```

Do not run these commands unless local commits were explicitly authorized.

---

### Task 2: Bounded candidate sources and deterministic discovery

**Files:**
- Create: `src/NxDrawingPdfExporter.App/Runtime/Detection/INxInstallationCandidateSource.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/Detection/EnvironmentNxCandidateSource.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/Detection/RegistryNxCandidateSource.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/Detection/InstalledApplicationNxCandidateSource.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/Detection/NxInstallationDiscoveryService.cs`
- Create: `tests/NxDrawingPdfExporter.App.Tests/NxInstallationCandidateSourceTests.cs`
- Create: `tests/NxDrawingPdfExporter.App.Tests/NxInstallationDiscoveryServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 models and `NxInstallationValidator.ValidateAndDeduplicate(...)`.
- Produces: `INxInstallationCandidateSource.GetCandidatesAsync(CancellationToken)` and `INxInstallationDiscoveryService.DetectAsync(string? savedRoot, CancellationToken)` / `ValidateManual(string)`.

- [ ] **Step 1: Write failing source tests**

Add tests for environment normalization and bounded registry values:

```csharp
[TestMethod]
public async Task EnvironmentSource_UgiiRootDirectory_ReturnsParentNxRoot()
{
    string root = Path.Combine(scratch, "NX Custom");
    string ugii = Path.Combine(root, "UGII") + Path.DirectorySeparatorChar;
    var source = new EnvironmentNxCandidateSource(name =>
        name == "UGII_ROOT_DIR" ? ugii : null);

    IReadOnlyList<NxInstallationCandidate> candidates =
        await source.GetCandidatesAsync(CancellationToken.None);

    Assert.HasCount(1, candidates);
    Assert.AreEqual(root, candidates[0].RootDirectory);
    Assert.AreEqual(NxCandidateSource.Environment, candidates[0].Source);
}
```

For registry sources, inject a reader delegate returning only strings from these bounded locations:

```text
HKLM (Registry64 and Registry32)\SOFTWARE\Unigraphics Solutions\NX\*\UGII_BASE_DIR
HKLM (Registry64 and Registry32)\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*\InstallLocation
```

The uninstall source must accept an entry only when `DisplayName` identifies Siemens NX 10.0; it may yield a candidate, but the Task 1 DLL check remains authoritative.

- [ ] **Step 2: Write failing discovery tests**

```csharp
[TestMethod]
public async Task DetectAsync_MultipleValidCandidates_SelectsValidSavedRoot()
{
    NxInstallation first = ValidInstallation(@"C:\NX-A");
    NxInstallation second = ValidInstallation(@"E:\NX-B");
    var sut = DiscoveryReturning(first, second);

    NxInstallationDiscoveryResult result =
        await sut.DetectAsync(@"e:\nx-b\", CancellationToken.None);

    Assert.AreEqual(second.RootDirectory, result.SelectedInstallation!.RootDirectory);
    Assert.IsFalse(result.RequiresSelection);
}

[TestMethod]
public async Task DetectAsync_SourceThrows_ContinuesAndReportsSanitizedIssue()
{
    var throwing = new StubCandidateSource(new UnauthorizedAccessException("private path"));
    var valid = new StubCandidateSource(new NxInstallationCandidate(validRoot, NxCandidateSource.Environment));
    var sut = new NxInstallationDiscoveryService(new[] { throwing, valid }, validator);

    NxInstallationDiscoveryResult result =
        await sut.DetectAsync(null, CancellationToken.None);

    Assert.IsTrue(result.IsReady);
    Assert.AreEqual(NxDetectionIssueCode.CandidateSourceFailed, result.Issues.Single().Code);
    Assert.DoesNotContain("private path", result.Issues.Single().UserMessage);
}
```

Also test: zero candidates; one valid candidate auto-selected; multiple candidates without saved match require selection; saved path is inserted before sources; invalid saved path does not block a later valid candidate; cancellation propagates; automatic and manual validation share the same validator.

- [ ] **Step 3: Run discovery tests and verify RED**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~NxInstallationCandidateSourceTests|FullyQualifiedName~NxInstallationDiscoveryServiceTests"
```

Expected: compilation fails on missing source/discovery types.

- [ ] **Step 4: Implement the candidate source interfaces**

```csharp
public interface INxInstallationCandidateSource
{
    Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(
        CancellationToken cancellationToken);
}

public interface INxInstallationDiscoveryService
{
    Task<NxInstallationDiscoveryResult> DetectAsync(
        string? savedRoot,
        CancellationToken cancellationToken);

    NxInstallationDiscoveryResult ValidateManual(string rootDirectory);
}
```

2026-09-10 authorized conflict resolution: use saved configuration, NX registry entries,
installed-application entries, and process environment variables only. Remove the
historical default-path source and its enum value. When those sources yield no valid
installation, offer manual selection through the shared validator. Do not encode or
reconstruct the old developer root to evade the release audit. Registry APIs must open
keys with `writable: false`. Do not add start-menu parsing or fixed-drive enumeration.

- [ ] **Step 5: Implement discovery and selection**

```csharp
public async Task<NxInstallationDiscoveryResult> DetectAsync(
    string? savedRoot,
    CancellationToken cancellationToken)
{
    var candidates = new List<NxInstallationCandidate>();
    var sourceIssues = new List<NxDetectionIssue>();
    if (!string.IsNullOrWhiteSpace(savedRoot))
        candidates.Add(new(savedRoot, NxCandidateSource.SavedConfiguration));

    foreach (INxInstallationCandidateSource source in sources)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { candidates.AddRange(await source.GetCandidatesAsync(cancellationToken)); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (IsExpectedSourceError(error))
        {
            sourceIssues.Add(new(
                NxDetectionIssueCode.CandidateSourceFailed,
                "一个 NX 安装来源无法读取，已继续检查其他来源。",
                Diagnostic: error.GetType().Name));
        }
    }

    NxInstallationDiscoveryResult validated = validator.ValidateAndDeduplicate(candidates);
    NxInstallation? selected = Select(validated.Installations, savedRoot);
    return validated with
    {
        Issues = sourceIssues.Concat(validated.Issues).ToArray(),
        SelectedInstallation = selected,
    };
}
```

`Select(...)` chooses the only valid installation, otherwise an exact normalized saved-root match, otherwise `null`. `ValidateManual(...)` returns one selected installation only when the shared validator accepts the candidate.

- [ ] **Step 6: Run focused tests and verify GREEN**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~NxInstallationCandidateSourceTests|FullyQualifiedName~NxInstallationDiscoveryServiceTests"
```

- [ ] **Step 7: Approval-gated local commit**

```powershell
git add src/NxDrawingPdfExporter.App/Runtime/Detection tests/NxDrawingPdfExporter.App.Tests/NxInstallationCandidateSourceTests.cs tests/NxDrawingPdfExporter.App.Tests/NxInstallationDiscoveryServiceTests.cs
git commit -m "feat: discover NX 10 from bounded local sources"
```

---

### Task 3: Atomic per-user NX settings

**Files:**
- Create: `src/NxDrawingPdfExporter.App/Configuration/NxSettings.cs`
- Create: `src/NxDrawingPdfExporter.App/Configuration/INxSettingsStore.cs`
- Create: `src/NxDrawingPdfExporter.App/Configuration/JsonNxSettingsStore.cs`
- Create: `tests/NxDrawingPdfExporter.App.Tests/NxSettingsStoreTests.cs`

**Interfaces:**
- Consumes: a validated `NxInstallation.RootDirectory` from Task 1.
- Produces: `INxSettingsStore.LoadAsync(CancellationToken)` and `SaveAsync(NxSettings, CancellationToken)`.

- [ ] **Step 1: Write failing settings tests**

```csharp
[TestMethod]
public async Task SaveThenLoad_UnicodePath_RoundTripsSchema1()
{
    string path = Path.Combine(scratch, "LocalAppData", "NxDrawingPdfExporter", "settings.json");
    var sut = new JsonNxSettingsStore(path);

    await sut.SaveAsync(new NxSettings(1, @"E:\西门子\NX 10.0"), CancellationToken.None);
    NxSettingsLoadResult loaded = await sut.LoadAsync(CancellationToken.None);

    Assert.AreEqual(NxSettingsLoadStatus.Loaded, loaded.Status);
    Assert.AreEqual(@"E:\西门子\NX 10.0", loaded.Settings.NxRootDirectory);
    Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".settings.json.*.tmp"));
}
```

Also test missing file, empty/invalid JSON, missing schema, unsupported schema, cancellation, replacement failure preserving the old target, and cleanup limited to the store-owned temporary file.

- [ ] **Step 2: Run settings tests and verify RED**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~NxSettingsStoreTests"
```

- [ ] **Step 3: Implement the settings contracts**

```csharp
public sealed record NxSettings(int SchemaVersion = 1, string? NxRootDirectory = null);

public enum NxSettingsLoadStatus
{
    Missing,
    Loaded,
    InvalidJson,
    UnsupportedSchema,
    ReadFailed,
}

public sealed record NxSettingsLoadResult(
    NxSettings Settings,
    NxSettingsLoadStatus Status,
    string? Diagnostic = null);

public interface INxSettingsStore
{
    Task<NxSettingsLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(NxSettings settings, CancellationToken cancellationToken);
}
```

The production path is composed with:

```csharp
Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "NxDrawingPdfExporter",
    "settings.json")
```

- [ ] **Step 4: Implement UTF-8 atomic save and tolerant load**

Serialize camel-case UTF-8 with a final newline. Create a unique file named
`.settings.json.<guid>.tmp` in the same directory, write with `FileOptions.WriteThrough`, flush, then call `File.Move(temp, target, overwrite: true)`. The `finally` block may delete only that exact owned temp path. Invalid content returns a load status and empty settings; it does not rename or delete the user's file.

Give `JsonNxSettingsStore` an internal constructor that accepts an `Action<string, string>` replacement seam. The public/default constructor supplies `(temp, target) => File.Move(temp, target, overwrite: true)`. The replacement-failure test injects an action that throws `IOException`, asserts the pre-existing target bytes are unchanged, and asserts only the newly owned temp file was cleaned. This seam is covered by the `InternalsVisibleTo` entry added in Task 1.

```csharp
public async Task SaveAsync(NxSettings settings, CancellationToken cancellationToken)
{
    if (settings.SchemaVersion != 1)
        throw new ArgumentOutOfRangeException(nameof(settings));

    Directory.CreateDirectory(directory);
    string temp = Path.Combine(directory, $".settings.json.{Guid.NewGuid():N}.tmp");
    try
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        await using var stream = new FileStream(
            temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(json, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        moveIntoPlace(temp, settingsPath);
    }
    finally
    {
        try { File.Delete(temp); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
```

- [ ] **Step 5: Run focused tests and verify GREEN**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~NxSettingsStoreTests"
```

- [ ] **Step 6: Approval-gated local commit**

```powershell
git add src/NxDrawingPdfExporter.App/Configuration tests/NxDrawingPdfExporter.App.Tests/NxSettingsStoreTests.cs
git commit -m "feat: persist validated NX selection atomically"
```

---

### Task 4: Controller orchestration and immutable Worker launch selection

**Files:**
- Modify: `src/NxDrawingPdfExporter.App/ApplicationController.cs`
- Modify: `tests/NxDrawingPdfExporter.App.Tests/ApplicationControllerTests.cs`

**Interfaces:**
- Consumes: `INxInstallationDiscoveryService`, `INxSettingsStore`, `NxInstallationDiscoveryResult`, and existing `IWorkerProcessLauncher`.
- Produces: `RefreshNxStatusAsync()`, `SelectDetectedNxAsync(string)`, `SelectManualNxAsync(string)`, `NxDiscovery`, `SelectedNx`, and `IsNxDetectionBusy`.

- [ ] **Step 1: Write failing controller tests**

```csharp
[TestMethod]
public async Task RefreshNxStatusAsync_UsesSavedRootAndPublishesReadyState()
{
    settings.LoadResult = new NxSettingsLoadResult(new NxSettings(1, @"E:\NX"), NxSettingsLoadStatus.Loaded);
    discovery.Result = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
    var controller = NewController();

    await controller.RefreshNxStatusAsync();

    Assert.AreEqual(@"E:\NX", discovery.LastSavedRoot);
    Assert.AreEqual(@"E:\NX", controller.SelectedNx!.RootDirectory);
    Assert.IsFalse(controller.IsNxDetectionBusy);
}

[TestMethod]
public async Task RunAsync_UsesSelectedCustomLauncher_NotV1Constant()
{
    var controller = NewReadyController(@"E:\Apps\NX10\UGII\run_managed.exe");
    controller.ScanFolderPath = inputDirectory;

    await controller.RunAsync();

    Assert.AreEqual(
        @"E:\Apps\NX10\UGII\run_managed.exe",
        launcher.LaunchCalls.Single().NxLauncherPath);
}
```

Add tests for: detection busy transitions and events; no selected installation blocks run before Job creation; multiple candidates require explicit selection; selecting a detected candidate saves it; invalid manual selection preserves current valid selection and saved value; save failure keeps the in-memory valid selection but exposes a warning; switching/re-detection is rejected while a batch is busy; each run captures one immutable launcher path.

- [ ] **Step 2: Run controller tests and verify RED**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~ApplicationControllerTests"
```

- [ ] **Step 3: Extend `ApplicationServices` and controller state**

```csharp
public INxInstallationDiscoveryService NxDiscovery { get; init; } =
    NxInstallationComposition.CreateDefault();
public INxSettingsStore NxSettings { get; init; } =
    JsonNxSettingsStore.CreateDefault();

public NxInstallationDiscoveryResult NxDiscovery { get; private set; } =
    new(Array.Empty<NxInstallation>(), Array.Empty<NxDetectionIssue>(), null);
public NxInstallation? SelectedNx => NxDiscovery.SelectedInstallation;
public bool IsNxDetectionBusy { get; private set; }
public string NxStatusMessage { get; private set; } = "尚未检测 NX。";
```

Remove the controller-owned `new NxInstallationDetector()` and the `NxDetectionOverride` test seam. Tests inject the discovery service and settings store through `ApplicationServices`.

- [ ] **Step 4: Implement asynchronous refresh and selection**

`RefreshNxStatusAsync()` loads settings, raises the busy state, calls discovery, formats a stable user message, and clears busy state in `finally`. `SelectDetectedNxAsync(root)` may select only an installation already present in the current result. `SelectManualNxAsync(root)` calls `ValidateManual`, changes state only on success, and saves only a validated root.

```csharp
public async Task RefreshNxStatusAsync(CancellationToken cancellationToken = default)
{
    if (IsBusy) throw new InvalidOperationException("运行期间不能重新检测 NX。");
    IsNxDetectionBusy = true;
    NxStatusMessage = "正在检测 NX…";
    Raise();
    try
    {
        NxSettingsLoadResult settings = await services.NxSettings.LoadAsync(cancellationToken);
        NxDiscovery = await services.NxDiscovery.DetectAsync(
            settings.Settings.NxRootDirectory, cancellationToken);
        NxStatusMessage = FormatNxStatus(NxDiscovery, settings.Status);
    }
    finally
    {
        IsNxDetectionBusy = false;
        Raise();
    }
}
```

At the beginning of `RunCore`, use the already selected immutable object. Do not silently re-run detection inside a batch:

```csharp
NxInstallation? selectedNx = SelectedNx;
if (selectedNx is null)
{
    ProgressText = "没有经过验证的 NX 10.0.0.24，无法开始导出。";
    return;
}

string launcherPathForRun = selectedNx.LauncherPath;
```

Pass `launcherPathForRun` into `WorkerLaunchRequest.NxLauncherPath`.

- [ ] **Step 5: Run controller tests and verify GREEN**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~ApplicationControllerTests"
```

- [ ] **Step 6: Run all App tests**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore
```

- [ ] **Step 7: Approval-gated local commit**

```powershell
git add src/NxDrawingPdfExporter.App/ApplicationController.cs tests/NxDrawingPdfExporter.App.Tests/ApplicationControllerTests.cs
git commit -m "feat: bind each export run to a validated NX installation"
```

---

### Task 5: NX selection controls and DPI-safe WinForms layout

**Files:**
- Modify: `src/NxDrawingPdfExporter.App/MainForm.cs`
- Modify: `src/NxDrawingPdfExporter.App/MainForm.Designer.cs`
- Create: `tests/NxDrawingPdfExporter.App.Tests/MainFormStateTests.cs`

**Interfaces:**
- Consumes: Task 4 controller methods and state.
- Produces: automatic startup detection, candidate selection, manual folder validation, re-detection, and a responsive layout suitable for human DPI QA.

- [ ] **Step 1: Write failing state tests for UI decisions**

Keep WinForms plumbing thin by extracting an internal pure projection in `MainForm.cs`:

```csharp
internal sealed record MainFormNxViewState(
    string StatusText,
    bool ShowCandidateList,
    bool EnableNxActions,
    bool EnableStart);

internal static MainFormNxViewState BuildNxViewState(ApplicationController controller)
{
    return new(
        controller.NxStatusMessage,
        controller.NxDiscovery.Installations.Count > 1,
        !controller.IsBusy && !controller.IsNxDetectionBusy,
        !controller.IsBusy && !controller.IsNxDetectionBusy && controller.SelectedNx is not null);
}
```

Test detection, multiple-candidate, ready, and batch-busy states:

```csharp
[TestMethod]
public void BuildNxViewState_MultipleCandidates_DisablesStartAndShowsSelection()
{
    ApplicationController controller = ControllerWithMultipleNxCandidates();

    MainFormNxViewState state = MainForm.BuildNxViewState(controller);

    Assert.IsTrue(state.ShowCandidateList);
    Assert.IsFalse(state.EnableStart);
}
```

- [ ] **Step 2: Run the UI state tests and verify RED**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore --filter "FullyQualifiedName~MainFormStateTests"
```

- [ ] **Step 3: Replace the NX environment row**

Add these controls in `MainForm.Designer.cs`:

```csharp
private Label labelNxStatus = null!;
private ComboBox comboNxInstallations = null!;
private Button buttonSelectNx = null!;
private Button buttonRefreshNx = null!;
private Button buttonBrowseNx = null!;
```

Build `groupNx` with a two-row `TableLayoutPanel`: a wrapping status label in row 0 and a `FlowLayoutPanel` containing the candidate combo and buttons in row 1. Candidate entries display root plus `10.0.0.24`; the bound value remains the `NxInstallation` object. The combo and “使用所选 NX” button are visible when there are multiple valid installations, including after one is selected (aligned with design 7.1).

- [ ] **Step 4: Bind asynchronous detection and manual selection**

Do not start detection in the constructor. Use the form's `Shown` event and one awaited method:

```csharp
Shown += async (_, _) => await RefreshNxAsync();
buttonRefreshNx.Click += async (_, _) => await RefreshNxAsync();
buttonSelectNx.Click += async (_, _) =>
{
    if (comboNxInstallations.SelectedItem is NxInstallation selected)
        await controller.SelectDetectedNxAsync(selected.RootDirectory);
};
buttonBrowseNx.Click += async (_, _) => await BrowseNxAsync();
```

`BrowseNxAsync()` opens `FolderBrowserDialog` with the description “请选择包含 UGII 文件夹的 NX 10.0 根目录”. On validation failure, show the result's stable user message and keep the prior valid selection. Catch exceptions only at this UI boundary and display one-line messages; cancellation closes quietly.

- [ ] **Step 5: Replace fixed coordinate panels with responsive layouts**

Replace the fixed-coordinate `panelScan`, `panelManual`, and `panelUnified` children with nested table layouts. Use percentage columns for expanding text/list controls and `AutoSize` columns for buttons. Set list views and progress bars to `Dock = Fill`; set `AutoScroll = true` on the main layout host; set a tested minimum client size; preserve `AutoScaleMode.Dpi`, `ApplicationHighDpiMode=PerMonitorV2`, and “Microsoft YaHei UI”.

Do not assert visual correctness from code. The code acceptance for this step is: no fixed `Left`/`Top` assignments remain for the affected input/output/NX controls, the project builds, and the user later completes Task 9 screenshots.

- [ ] **Step 6: Set deterministic accessibility and tab behavior**

Assign TabIndex in this order: NX candidate/selection/re-detect/manual browse; input mode and controls; output mode and controls; existing-file policy; preflight; start/cancel; results/log actions. Set accessible names for the new combo and buttons. Disabled controls must remain visible enough to communicate state.

- [ ] **Step 7: Run tests and build**

```powershell
& $dotnet test tests\NxDrawingPdfExporter.App.Tests -c Release --no-restore
& $dotnet build src\NxDrawingPdfExporter.App\NxDrawingPdfExporter.App.csproj -c Release --no-restore
```

Expected: all App tests pass and the build has zero errors. Do not launch the GUI; visual validation belongs to Task 9 and the user.

- [ ] **Step 8: Approval-gated local commit**

```powershell
git add src/NxDrawingPdfExporter.App/MainForm.cs src/NxDrawingPdfExporter.App/MainForm.Designer.cs tests/NxDrawingPdfExporter.App.Tests/MainFormStateTests.cs
git commit -m "feat: add DPI-safe NX installation selection UI"
```

---

### Task 6: Remove fixed NX roots from release payloads and mark V2 metadata

**Files:**
- Modify: `Directory.Build.props`
- Modify: `tools/inspect-release.ps1`
- Modify: `tools/test-inspect-release.ps1`
- Modify: `tools/publish-portable.ps1`
- Modify: `tools/test-publish-portable.ps1`
- Modify: `README.md`
- Modify only after evidence: `docs/verification/release/known-limitations.md`
- Test: `tools/test-inspect-release.ps1`
- Test: `tools/test-publish-portable.ps1`

**Interfaces:**
- Consumes: V2 binaries and current offline publishing workflow.
- Produces: version `2.0.0`, a package with no developer/NX-root literal, and release instructions that describe discovery/manual fallback accurately.

- [ ] **Step 1: Add a failing fixed-NX-root inspection fixture**

In `tools/test-inspect-release.ps1`, isolate the fixed-root case so it cannot pass merely because another file contains `E:\Codex`:

```powershell
$fixedNxPackage = Join-Path $fixtureRoot 'fixed-nx-root'
New-Item -ItemType Directory -Path $fixedNxPackage | Out-Null
[System.IO.File]::WriteAllBytes(
    (Join-Path $fixedNxPackage 'app.dll'),
    [System.Text.Encoding]::Unicode.GetBytes('D:\Program Files\Siemens\NX 10.0'))
$fixedNxExit = Invoke-Inspector -Package $fixedNxPackage -Manifest (Join-Path $fixtureRoot 'fixed-nx.sha256')
if ($fixedNxExit -eq 0) { throw 'V2 检查失败：固定 NX 根目录未被拒绝。' }
```

- [ ] **Step 2: Run the inspector self-test and verify RED**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-inspect-release.ps1
```

Expected: the new isolated fixed-root case fails because V1 currently removes that literal through `$approvedPathPrefixes`.

- [ ] **Step 3: Remove the V1 NX-root exemption**

Change the reviewed prefix list to third-party build roots only:

```powershell
$approvedPathPrefixes = @(
    'D:\a\_work\',
    'D:\repos\empira\'
)
$forbiddenLiterals = @(
    'E:\Codex',
    'D:\Program Files\Siemens\NX 10.0',
    'nx-step-launcher'
)
```

Keep the generic developer-path, Siemens DLL, private-file, UTF-8 manifest, and tamper tests unchanged.

- [ ] **Step 4: Update version and package instructions**

Set:

```xml
<Version>2.0.0</Version>
<AssemblyVersion>2.0.0.0</AssemblyVersion>
<FileVersion>2.0.0.0</FileVersion>
```

Update the generated `说明.txt` to say the app automatically detects an exact NX 10.0.0.24 installation and offers “手动选择 NX 目录” when detection cannot choose. It must not mention a drive letter.

Update README status to “V2 implementation candidate” until Tasks 8–10 pass. Do not claim Issue #2/#3 complete during implementation.

- [ ] **Step 5: Verify tooling GREEN**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-inspect-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-publish-portable.ps1
```

Expected: both self-tests pass. The publish policy must still contain `--no-restore` for test, build, and publish commands.

- [ ] **Step 6: Approval-gated local commit**

```powershell
git add Directory.Build.props tools/inspect-release.ps1 tools/test-inspect-release.ps1 tools/publish-portable.ps1 tools/test-publish-portable.ps1 README.md
git commit -m "release: remove fixed NX path assumptions for v2"
```

Do not update `known-limitations.md` to passed yet; that waits for real evidence.

---

### Task 7: Full automated regression and portable candidate package

**Files:**
- No product source creation expected.
- Produces only ignored outputs under `artifacts/release/` and existing build directories.

**Interfaces:**
- Consumes: Tasks 1–6.
- Produces: a locally verified candidate package and SHA-256 manifest for human validation.

- [x] **Step 1: Run the entire Release test suite**

```powershell
& $dotnet test NxDrawingPdfExporter.slnx -c Release --no-restore
```

Expected: zero failed tests. Record exact project counts. Correction from current
baseline evidence: known `NU1702` is the net10 Worker.Tests reference to net48 Worker,
not PDFsharp. The identical warning is documented in the V2 verification README;
any new warning remains a stop condition. 2026-09-13 r2: 200/200 passed.

- [x] **Step 2: Build the entire solution**

```powershell
& $dotnet build NxDrawingPdfExporter.slnx -c Release --no-restore
```

Expected: zero errors. Do not summarize a partial project build as a full build.

- [x] **Step 3: Run static policy checks**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-publish-portable.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-inspect-release.ps1
rg -n -i "HttpClient|WebClient|HttpWebRequest|TcpClient|Socket|Download(File|String)|https?://" src tools\publish-portable.ps1
rg -n -F "D:\Program Files\Siemens\NX 10.0" src
```

Expected: both scripts pass; the network API scan has no unexplained product-runtime hit; the fixed NX root has no `src` hit.

- [x] **Step 4: Build and inspect the candidate package**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\publish-portable.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\inspect-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\inspect-release.ps1 -VerifyManifestOnly
```

Expected: `PUBLISH OK`, `INSPECT OK`, and `MANIFEST VERIFY OK`. Record package file count and SHA-256 of the delivered ZIP/package artifact. Do not copy private samples into the clean package.

- [x] **Step 5: Audit the intended diff**

```powershell
git diff --check
git status --short
git diff --stat
git diff -- src tests tools Directory.Build.props README.md docs/superpowers
```

Confirm every changed path belongs to V2 and no pre-existing untracked handoff/cache files are staged.

- [ ] **Step 6: Approval-gated local checkpoint commit**

If Tasks 1–6 were not committed separately and the user now authorizes one local implementation commit:

```powershell
git add Directory.Build.props README.md src tests tools docs/superpowers/specs/2026-09-08-nx-drawing-pdf-exporter-v2-design.md docs/superpowers/plans/2026-09-08-nx-drawing-pdf-exporter-v2-implementation.md docs/prompts/2026-09-08-terra-v2-implementation-prompt.md docs/prompts/2026-09-08-colleague-v2-validation-prompt.md docs/verification/v2/colleague-manual-checklist.md
git commit -m "feat: add portable NX discovery for v2"
```

Do not stage unrelated files and do not push.

---

### Task 8: Local real-NX functional gate before GUI QA

**Files:**
- Inputs/outputs remain ignored under `samples/private/` and `artifacts/`.
- Create or update after successful evidence only: `docs/verification/v2/README.md`.

**Interfaces:**
- Consumes: Task 7 candidate package and the protected local drawing/model pair.
- Produces: real local evidence that dynamic discovery launches the existing Worker correctly.

- [x] **Step 1: Prepare a user-operated validation checklist**

Terra must write exact commands for the user to record drawing/model SHA-256, copy the candidate package to a fresh directory, and collect the selected NX root/version without exposing private names in tracked files. Terra must not launch the GUI itself.

- [ ] **Step 2: User records fresh pre-run hashes**

The user runs the supplied PowerShell commands against the exact drawing and associated model. Raw paths and hashes stay under ignored `artifacts/v2-local-validation/`. If either file is missing or another NX session has it modified, stop.

- [ ] **Step 3: User launches the clean candidate and performs one export**

Expected GUI observations: automatic selection of the local NX 10.0.0.24; displayed root equals the machine's actual root; start enabled only after validation; one controlled export reaches a terminal success; no NX save prompt appears.

- [ ] **Step 4: User records post-run hashes and PDF evidence**

Both hashes must equal their pre-run values. Inspect the PDF header, expected page count, original page size, and every rendered page. Any mismatch is a hard failure and blocks Task 9.

- [ ] **Step 5: Terra validates returned evidence without GUI control**

Terra checks the user's files/reported observations, relevant result JSON, sanitized log, and hashes. A screenshot is supporting evidence; the structured result, PDF inspection, and hashes remain authoritative.

---

### Task 9: User-operated 100/125/150% DPI and physical-offline QA

**Files:**
- Read: `docs/verification/v2/colleague-manual-checklist.md`
- Raw screenshots/logs: ignored `artifacts/v2-local-validation/`
- Create or update after successful evidence only: `docs/verification/v2/README.md`

**Interfaces:**
- Consumes: the exact Task 7 package/manifest and Task 8 passing local gate.
- Produces: EV-04 and EV-05 evidence. No automated test substitutes for this task.

- [ ] **Step 1: Give the user the DPI matrix before any GUI work**

For each Windows scaling value 100%, 125%, and 150%, specify the exact display used, whether sign-out/restart is required, and these screenshots: full window with NX ready; scan-folder mode; manual-file mode; unified-output long path; preflight list; running/disabled controls; final results. Each image must include proof of the active scaling value and the complete app window.

- [ ] **Step 2: User performs each DPI row**

The user checks clipping, overlap, inaccessible controls, Chinese truncation, long-path behavior, list resizing, Tab order, and enable/disable transitions. On the first defect, stop that row, record the triggering state, and return to Task 5 with a new failing state test where deterministic behavior is involved.

- [ ] **Step 3: Give the user the physical-offline checklist**

Use the same package hash as Task 7. Require fresh drawing/model pre-hashes, a clean extraction outside the repo, network disconnection by the user, GUI launch, automatic NX discovery, one controlled export, PDF inspection, no network/download/authentication prompt, post-hashes, and only then network restoration.

- [ ] **Step 4: User performs the offline run and returns evidence**

Any network prompt, discovery failure, partial official PDF, or source hash change fails the gate. The user stops and preserves the run directory instead of repeating or changing system/NX configuration.

- [ ] **Step 5: Record the truthful result**

Only after all three DPI rows and the disconnected export pass may `docs/verification/release/known-limitations.md` mark EV-04/EV-05 resolved. Otherwise, document the exact remaining row/gate and keep the GitHub Issues open.

---

### Task 10: Colleague-machine non-default-path validation

**Files:**
- Read: `docs/prompts/2026-09-08-colleague-v2-validation-prompt.md`
- Read: `docs/verification/v2/colleague-manual-checklist.md`
- Raw colleague evidence remains on the colleague machine or under ignored `artifacts/v2-colleague-validation/`.
- Create or update after successful sanitized review only: `docs/verification/v2/README.md`.

**Interfaces:**
- Consumes: the exact Task 7 candidate artifact and manifest.
- Produces: independent evidence that the colleague's unknown non-default root is automatically discovered and used.

- [ ] **Step 1: Send the frozen artifact, manifest, Codex prompt, and human checklist**

Do not rebuild between local and colleague QA. Record the artifact SHA-256 before transfer. The colleague verifies the received hash before extraction.

- [ ] **Step 2: Colleague Codex performs bounded read-only diagnostics**

It records Windows architecture/version, exact `NXOpen.dll` file version, whether the bounded registry/environment sources expose a candidate, and the selected `run_managed.exe`. It must not dump full registry/environment contents or modify the machine.

- [ ] **Step 3: Colleague performs the GUI and export steps**

The colleague—not Codex—operates the GUI. Automatic discovery must succeed without first using manual selection. If it fails, capture diagnostics; then test manual selection separately. Manual success proves only fallback behavior and does not pass automatic discovery.

- [ ] **Step 4: Colleague and Codex return sanitized evidence**

Required: artifact hash match, version `10.0.0.24`, automatic-discovery source/result, proof that the launched path belongs to that installation, structured export result, PDF inspection, and equal drawing/model before/after hashes. Redact private basenames, business directories, drawing content, and hash values from tracked summaries.

- [ ] **Step 5: Terra evaluates the gate**

If any required item is missing, label the build “V2 candidate; colleague-path validation incomplete.” Do not claim the original field problem fixed and do not close/update the remote issue without user authorization.

---

### Task 11: Evidence reconciliation and independent review handoff

**Files:**
- Modify: `README.md`
- Modify: `docs/verification/release/known-limitations.md`
- Create or modify: `docs/verification/v2/README.md`
- Create: `docs/handoffs/2026-09-08-v2-implementation-review.md`
- Modify: `docs/superpowers/plans/2026-09-08-nx-drawing-pdf-exporter-v2-implementation.md` (checkboxes only when evidence exists)

**Interfaces:**
- Consumes: Tasks 7–10 test, build, package, local, DPI, offline, and colleague evidence.
- Produces: one truthful status source and a reviewable handoff. It does not push or publish.

- [x] **Step 1: Reconcile every completion requirement**

For each design section 15 item, link a command result or sanitized evidence artifact. Leave unmet plan boxes unchecked. Do not convert “not run,” “user will run,” or a screenshot plan into PASS.

- [x] **Step 2: Update documentation to the exact state**

README may say “V2 complete” only when Tasks 7–10 all pass. `known-limitations.md` must retain any untested machine/configuration boundary. `docs/verification/v2/README.md` records commands, counts, package hash, sanitized installation-source facts, DPI matrix status, offline status, colleague status, and source-integrity outcome without private values.

- [x] **Step 3: Prepare the independent-review handoff**

The handoff must include: intended commit/diff range; exact changed files; RED→GREEN evidence; full test counts; build warnings/errors; package/manifest result; current Git status; live NX evidence; human DPI/offline evidence; colleague evidence; unverified items; and explicit no-push/no-tag/no-release limits.

- [x] **Step 4: Run final read-only verification**

2026-09-13 checkpoint: the full tests/build and policy tests ran again during
candidate publication. Verify the current candidate with explicit paths below;
the script defaults still point to the preserved 2026-09-10 package. Final live
evidence reconciliation remains pending even though these automatic checks pass.

```powershell
& $dotnet test NxDrawingPdfExporter.slnx -c Release --no-restore
& $dotnet build NxDrawingPdfExporter.slnx -c Release --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-publish-portable.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-inspect-release.ps1
$reviewRoot = git rev-parse --show-toplevel
powershell -NoProfile -ExecutionPolicy Bypass -File tools\inspect-release.ps1 -PackageRoot "$reviewRoot\artifacts\release\v2-candidate-20260913-r2\clean-extraction" -ManifestPath "$reviewRoot\artifacts\release\v2-candidate-20260913-r2\manifest.sha256" -VerifyManifestOnly
git diff --check
git status --short --branch
```

Read every command's full output. Do not claim completion from exit code alone.

- [ ] **Step 5: Approval-gated documentation commit**

```powershell
git add README.md docs/verification/release/known-limitations.md docs/verification/v2/README.md docs/handoffs/2026-09-08-v2-implementation-review.md docs/superpowers/plans/2026-09-08-nx-drawing-pdf-exporter-v2-implementation.md
git commit -m "docs: record v2 verification and review handoff"
```

Do not push, tag, publish, close Issues, or create a GitHub Release.
