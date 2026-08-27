# NX Drawing PDF Exporter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 交付一个可解压运行的 Windows 11 x64 工具，借助本机 Siemens NX 10.0.0.24 批量将制图 PRT 中含实际 Drafting View 的 Drawing Sheet 按导航器顺序导出为同名多页 PDF。

**Architecture:** 采用双进程：自包含 .NET 10 WinForms GUI 负责发现、预检查、编排和结果显示；.NET Framework 4.8 x64 Worker 由 NX `run_managed.exe` 启动并独占 NXOpen。跨进程通过产品自有 JSON DTO 交换任务与结果；NX 无关规则放入 `Core`，GUI 永不引用 Siemens 程序集。

**Tech Stack:** C#；.NET SDK 10.0.400；WinForms `net10.0-windows`；Core/Contracts `netstandard2.0`；Worker `net48` x64；MSTest 4.0.2；PDFsharp Core 6.2.4；Siemens NXOpen 10.0.0.24 / NXOpen.UF 10.0.0.23（仅本机引用，`Private=false`）。

**Spec:** `docs/superpowers/specs/2026-08-26-nx-drawing-pdf-exporter-design.md`

## Global Constraints

- 只能修改 `E:\Codex\projects\nx-pdf-sample`；相邻 `nx-step-launcher` 只读。
- 先读完整 `AGENTS.md`、规格和本计划；不得重新发散产品范围。
- 不修改、保存、重命名、移动或提交 `samples/private/` 中任何文件。
- 每次 NX 真机测试前后分别记录制图 PRT 与关联模型 SHA-256；任一变化立即停止。
- 不修改 NX 安装、注册表、PATH、用户/系统环境变量或服务，不复制/分发 Siemens DLL。
- 不安装或下载 SDK、NuGet 包或工具。缺少依赖时，先提交来源、版本、落盘位置、许可证、网络与发布影响，等待用户明确授权。
- Worker 真机门槛 1 未通过前，不得实现 GUI；不得用模拟测试代替三道真机门槛。
- 所有 PRT 串行处理；不并行启动 NX Session。
- 不 push、不发布、不创建 GitHub Release。
- 每个任务采用红—绿—重构：先写失败测试，确认失败原因正确，再实现最小代码并复测。

## Verified Local Baseline

- 项目：`E:\Codex\projects\nx-pdf-sample`，独立 Git 仓库，`main` 基线提交 `22c3c21`。
- 可只读调用 SDK：`E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe`，SDK `10.0.400`。
- 系统 .NET Framework：Release `533509`，`Framework64\v4.0.30319`。
- NX 根目录：`D:\Program Files\Siemens\NX 10.0`。
- 启动器：`D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe`。
- 程序集：`UGII\managed\NXOpen.dll` `10.0.0.24`，`NXOpen.UF.dll` `10.0.0.23`。
- 已核对本机 XML API：`DrawingSheet.GetDraftingViews()`、`DrawingSheet.Open()`、`DraftingViewCollection.UpdateViews(...)`、`PartCollection.OpenDisplay(...)`、`PlotManager.CreatePrintPdfbuilder()`、`PlotSourceBuilder.SetSheets(...)`、`PrintPDFBuilder.Filename/Append/Commit()`。
- PDFsharp Core 6.2.4：MIT；目标包含 `netstandard2.0`；发布包需带许可证文本。它仅负责读取 PDF 元数据和页数，不参与 NX 输出。

## Standard Development Shell

每个新 PowerShell 会话仅设置进程内变量，不写全局环境：

```powershell
$taskRoot = 'E:\Codex\projects\nx-pdf-sample'
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $taskRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $taskRoot '.tools\nuget-http-cache'
Set-Location -LiteralPath $taskRoot
```

如果依赖获批且需要首次联网还原，只允许写入上述项目内缓存；不得写相邻项目或用户全局缓存。随后必须用 `--no-restore` 证明离线可构建。

## Requirement Traceability

| 已批准行为 | 落地任务 |
|---|---|
| 扫描文件夹、手动多选、包含子文件夹开关 | Task 4、10、11 |
| 跟随源目录、统一输出目录 | Task 4、10、11 |
| 跳过已有 PDF、安全覆盖已有 PDF | Task 4、7、11 |
| 纯模型、无有效页、名称冲突、取消、失败继续 | Task 5、9、11 |
| `GetDraftingViews()` 有效页判据、预制页跳过 | Task 5、6 |
| 导航器顺序、多页 PDF、原始图幅、白底黑线 | Task 6、8 |
| Worker 真机启动、样例导出、批量行为三道门槛 | Task 3、6/8、11 |
| 不保存源文件、前后哈希、安全临时副本 | Task 6、8、11 |
| 便携 GUI、多 DPI、干净解压、断网运行 | Task 10、12 |

---

### Task 1: Freeze toolchain, dependency policy, and solution boundaries

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `NuGet.Config`
- Create: `THIRD-PARTY-NOTICES.txt`
- Create: `NxDrawingPdfExporter.slnx`
- Create: `src/NxDrawingPdfExporter.Contracts/NxDrawingPdfExporter.Contracts.csproj`
- Create: `src/NxDrawingPdfExporter.Core/NxDrawingPdfExporter.Core.csproj`
- Create: `src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj`
- Create: `src/NxDrawingPdfExporter.Worker/NxDrawingPdfExporter.Worker.csproj`
- Create: `tools/NxDrawingPdfExporter.Probe/NxDrawingPdfExporter.Probe.csproj`
- Create: `tests/NxDrawingPdfExporter.Contracts.Tests/NxDrawingPdfExporter.Contracts.Tests.csproj`
- Create: `tests/NxDrawingPdfExporter.Core.Tests/NxDrawingPdfExporter.Core.Tests.csproj`
- Create: `tests/NxDrawingPdfExporter.App.Tests/NxDrawingPdfExporter.App.Tests.csproj`

- [x] **Step 1: Create and switch to the implementation branch**

```powershell
git status --short
git switch -c feature/nx-drawing-pdf-exporter-v1
```

Expected: the pre-switch status is clean; the new branch is based on `22c3c21` plus the approved documentation commit.

Result: clean `main` at `a9b2857`; branch `feature/nx-drawing-pdf-exporter-v1` created.

- [x] **Step 2: Pin SDK and deterministic build defaults**

`global.json` pins `10.0.400` with `rollForward: latestPatch`. `Directory.Build.props` enables nullable, deterministic builds, `TreatWarningsAsErrors`, and does not set a global target framework.

- [x] **Step 3: Pin only reviewed packages**

`Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="MSTest" Version="4.0.2" />
    <PackageVersion Include="PDFsharp" Version="6.2.4" />
  </ItemGroup>
</Project>
```

`NuGet.Config` must declare only `https://api.nuget.org/v3/index.json`; a restore that requires network is an explicit authorization gate, not an automatic step.

- [x] **Step 4: Record PDFsharp distribution impact**

`THIRD-PARTY-NOTICES.txt` includes PDFsharp name, version, project URL, MIT copyright/license text, and states that its managed runtime assemblies are included in the GUI distribution. No Siemens file appears in this notice or package.

- [x] **Step 5: Create project graph**

References must be exactly:

```text
App -> Core, Contracts, PDFsharp
Worker -> Core, Contracts, NXOpen, NXOpen.UF
Probe -> Contracts, NXOpen, NXOpen.UF
Core -> Contracts
Tests -> their production project
```

Task 1 只创建 App 的空项目边界，使解决方案与测试引用可还原；任何 WinForms 窗体、控制器或产品 GUI 行为都必须等 Worker gate 1 通过后再写。

- [x] **Step 6: Restore/build boundary check**

```powershell
& $taskDotnet restore .\NxDrawingPdfExporter.slnx
& $taskDotnet build .\NxDrawingPdfExporter.slnx -c Debug --no-restore
```

Expected: either clean success, or a precise missing-package stop. No automatic download is allowed without the user's separate approval.

Result: precise missing-package stop with zero network traffic (restore executed against an empty local feed override). Local caches seeded offline from the adjacent project's cache cover MSTest 4.0.2 full closure incl. transitive deps. Remaining machine-missing packages pending user authorization: `PDFsharp 6.2.4` (approved dependency), `NETStandard.Library 2.0.0` and `Microsoft.NETFramework.ReferenceAssemblies.net48 1.0.3` (SDK-required build infrastructure; this machine has no VS targeting packs). All land in repo-local `.tools/nuget-packages`.

- [x] **Step 7: Commit**

```powershell
git add global.json Directory.Build.props Directory.Packages.props NuGet.Config THIRD-PARTY-NOTICES.txt NxDrawingPdfExporter.slnx src tests tools
git commit -m "build: scaffold exporter solution"
```

Result: committed together with the updated plan checkboxes (Task 1 steps 1–7).

### Task 2: Define the versioned job/result protocol

**Files:**
- Create: `src/NxDrawingPdfExporter.Contracts/ProtocolVersion.cs`
- Create: `src/NxDrawingPdfExporter.Contracts/JobRequest.cs`
- Create: `src/NxDrawingPdfExporter.Contracts/JobItem.cs`
- Create: `src/NxDrawingPdfExporter.Contracts/JobResult.cs`
- Create: `src/NxDrawingPdfExporter.Contracts/FileResult.cs`
- Create: `src/NxDrawingPdfExporter.Contracts/Enums.cs`
- Create: `src/NxDrawingPdfExporter.Contracts/JobJsonSerializer.cs`
- Test: `tests/NxDrawingPdfExporter.Contracts.Tests/JobJsonSerializerTests.cs`

- [x] **Step 1: Write failing round-trip and validation tests**

Cover Unicode/space paths, every result status, ordered job items, missing required fields, unknown protocol version, and a malformed JSON file. The test must assert that no plaintext exception stack is stored in user-facing `Message`.

Result: 11 tests in `JobJsonSerializerTests` covering all mandated cases; RED confirmed via CS0246 compile failure (DTOs absent).

- [x] **Step 2: Run the focused test and confirm RED**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Contracts.Tests\NxDrawingPdfExporter.Contracts.Tests.csproj --no-restore --filter FullyQualifiedName~JobJsonSerializerTests
```

Expected: compile/test failure because DTOs and serializer do not exist.

- [x] **Step 3: Implement stable DTOs**

Use `DataContractJsonSerializer` so both `netstandard2.0` and `net48` use the same protocol without another JSON dependency. Required shapes:

```csharp
public enum ExistingPdfPolicy { Skip, Overwrite }
public enum OutputMode { BesideSource, UnifiedDirectory }
public enum FileResultStatus
{
    Success, Overwritten, SkippedExisting, PureModel,
    NoValidSheets, NameConflict, Cancelled, Failed
}

[DataContract]
public sealed class JobRequest
{
    [DataMember(Order = 1, IsRequired = true)] public string ProtocolVersion { get; set; } = "1";
    [DataMember(Order = 2, IsRequired = true)] public string RunId { get; set; } = "";
    [DataMember(Order = 3, IsRequired = true)] public JobItem[] Items { get; set; } = Array.Empty<JobItem>();
    [DataMember(Order = 4, IsRequired = true)] public OutputMode OutputMode { get; set; }
    [DataMember(Order = 5)] public string? UnifiedOutputDirectory { get; set; }
    [DataMember(Order = 6, IsRequired = true)] public ExistingPdfPolicy ExistingPdfPolicy { get; set; }
    [DataMember(Order = 7, IsRequired = true)] public string ResultPath { get; set; } = "";
    [DataMember(Order = 8, IsRequired = true)] public string CancellationFlagPath { get; set; } = "";
}

[DataContract]
public sealed class JobItem
{
    [DataMember(Order = 1, IsRequired = true)] public string SourcePath { get; set; } = "";
    [DataMember(Order = 2, IsRequired = true)] public string FinalOutputPath { get; set; } = "";
    [DataMember(Order = 3, IsRequired = true)] public string WorkerTempOutputPath { get; set; } = "";
}
```

`FileResult` carries source/target/status/message, ordered exported/skipped sheet names, and elapsed milliseconds. `JobResult` carries run id, start/end UTC, cancellation state, ordered per-file results, and fatal worker error.

- [x] **Step 4: Implement atomic JSON writes**

Serialize to a same-directory unique temporary file, flush/close, then move/replace. Deserialization validates protocol version and required absolute paths.

- [x] **Step 5: Run GREEN and full contract tests**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Contracts.Tests\NxDrawingPdfExporter.Contracts.Tests.csproj --no-restore
```

Expected: all contract tests pass.

- [x] **Step 6: Commit**

```powershell
git add src/NxDrawingPdfExporter.Contracts tests/NxDrawingPdfExporter.Contracts.Tests
git commit -m "feat: define worker job protocol"
```

### Task 3: Prove NX 10 can launch a .NET Framework 4.8 managed worker

**Files:**
- Create: `tools/NxDrawingPdfExporter.Probe/Program.cs`
- Create: `tools/NxDrawingPdfExporter.Probe/ProbeReport.cs`
- Create: `tools/run-worker-probe.ps1`
- Create: `docs/verification/gate-1/README.md`

- [x] **Step 1: Configure exact NX references without redistribution**

Worker and Probe csproj files use explicit `HintPath` values below and `<Private>false</Private>`:

```xml
<Reference Include="NXOpen">
  <HintPath>D:\Program Files\Siemens\NX 10.0\UGII\managed\NXOpen.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="NXOpen.UF">
  <HintPath>D:\Program Files\Siemens\NX 10.0\UGII\managed\NXOpen.UF.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Assert x64 and `net48`. Add an MSBuild target that fails if either file is absent or if `NXOpen.dll` file version is not `10.0.0.24`.

- [x] **Step 2: Implement the minimal probe only**

The probe obtains real objects and writes one UTF-8 JSON report:

```csharp
var session = NXOpen.Session.GetSession();
var ufSession = NXOpen.UF.UFSession.GetUFSession();
if (session == null || ufSession == null) return 20;
```

Report process bitness, runtime version, current directory, argument echo, NX session availability, UF session availability, NX executable root, UTC timestamp, and exit code. Do not open any PRT yet.

- [x] **Step 3: Build and check no Siemens binaries were copied**

```powershell
& $taskDotnet build .\tools\NxDrawingPdfExporter.Probe\NxDrawingPdfExporter.Probe.csproj -c Debug --no-restore
Get-ChildItem .\tools\NxDrawingPdfExporter.Probe\bin\Debug -Recurse -Include NXOpen*.dll
```

Expected: build succeeds; the second command returns no files.

- [x] **Step 4: Discover and record the real `run_managed.exe` command syntax**

`tools/run-worker-probe.ps1` invokes only the verified local launcher and the built probe. It must capture exact command line, stdout, stderr, exit code, and report path under `docs/verification/gate-1/` while excluding usernames and sample paths from committed artifacts.

Expected gate result: exit code 0, valid JSON, both Session fields true, argument echo exact.

- [x] **Step 5: Hard stop on gate failure**

If NX rejects `net48`, cannot load the assembly, or cannot return both real sessions, stop all implementation. Record error and environment facts, then ask for a spec revision. Do not implement GUI or silently switch framework/launcher.

- [x] **Step 6: Commit successful gate evidence**

```powershell
git add tools/NxDrawingPdfExporter.Probe tools/run-worker-probe.ps1 docs/verification/gate-1
git commit -m "test: prove NX 10 managed worker launch"
```

### Task 4: Implement deterministic discovery and output preflight

**Files:**
- Create: `src/NxDrawingPdfExporter.Core/Input/InputDiscoveryService.cs`
- Create: `src/NxDrawingPdfExporter.Core/Input/IFileSystem.cs`
- Create: `src/NxDrawingPdfExporter.Core/Output/OutputPlanner.cs`
- Create: `src/NxDrawingPdfExporter.Core/Output/OutputPlanItem.cs`
- Test: `tests/NxDrawingPdfExporter.Core.Tests/InputDiscoveryServiceTests.cs`
- Test: `tests/NxDrawingPdfExporter.Core.Tests/OutputPlannerTests.cs`

- [ ] **Step 1: Write failing discovery tests**

Cover current-directory scan, recursive opt-in, `.prt` case-insensitivity, manual multi-select order, canonical-path de-duplication, Chinese/spaces/multiple dots, missing files, and inaccessible folders. Do not infer a drawing from `DWG_`.

- [ ] **Step 2: Write failing output-plan tests**

Cover source-basename preservation, beside-source/unified output, skip/overwrite, all colliding unified-output items marked `NameConflict`, target-directory validation, and no partial preflight mutation.

- [ ] **Step 3: Confirm RED**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~InputDiscoveryServiceTests|FullyQualifiedName~OutputPlannerTests"
```

- [ ] **Step 4: Implement the smallest pure services**

Key contract:

```csharp
public interface IInputDiscoveryService
{
    IReadOnlyList<string> ScanFolder(string folder, bool includeSubfolders);
    IReadOnlyList<string> NormalizeManualSelection(IEnumerable<string> paths);
}

public interface IOutputPlanner
{
    IReadOnlyList<OutputPlanItem> Plan(
        IReadOnlyList<string> sources,
        OutputMode mode,
        string? unifiedDirectory,
        ExistingPdfPolicy policy);
}
```

Preserve discovery/manual order; never sort by sheet or source filename unless the UI explicitly displays a separate view without altering job order.

- [ ] **Step 5: Run GREEN and commit**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore
git add src/NxDrawingPdfExporter.Core tests/NxDrawingPdfExporter.Core.Tests
git commit -m "feat: add input and output preflight"
```

### Task 5: Implement sheet-selection rules as pure logic

**Files:**
- Create: `src/NxDrawingPdfExporter.Core/Drawing/SheetFacts.cs`
- Create: `src/NxDrawingPdfExporter.Core/Drawing/SheetSelectionService.cs`
- Test: `tests/NxDrawingPdfExporter.Core.Tests/SheetSelectionServiceTests.cs`

- [ ] **Step 1: Write failing tests for the approved rule**

Tests must prove: zero sheets => pure model; sheets with zero drafting views => no valid sheets; one or more drafting views => exportable; input order retained; update/load error => file failure rather than blank/template classification.

- [ ] **Step 2: Confirm RED**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore --filter FullyQualifiedName~SheetSelectionServiceTests
```

- [ ] **Step 3: Implement explicit facts and decisions**

```csharp
public sealed class SheetFacts
{
    public int NavigatorIndex { get; set; }
    public string Name { get; set; } = "";
    public int DraftingViewCount { get; set; }
    public string? InspectionFailure { get; set; }
}

public sealed class SheetSelection
{
    public IReadOnlyList<int> ExportIndices { get; set; } = Array.Empty<int>();
    public IReadOnlyList<int> SkipIndices { get; set; } = Array.Empty<int>();
    public string? FatalFailure { get; set; }
}
```

Do not inspect borders, notes, tables, layer numbers, names, or prefixes. `DraftingViewCount >= 1` is the only positive rule.

- [ ] **Step 4: Run GREEN and commit**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore
git add src/NxDrawingPdfExporter.Core tests/NxDrawingPdfExporter.Core.Tests
git commit -m "feat: classify exportable drawing sheets"
```

### Task 6: Build the NX adapter and prove sheet order on the private sample

**Files:**
- Create: `src/NxDrawingPdfExporter.Worker/Program.cs`
- Create: `src/NxDrawingPdfExporter.Worker/NxPartProcessor.cs`
- Create: `src/NxDrawingPdfExporter.Worker/NxSheetInventory.cs`
- Create: `src/NxDrawingPdfExporter.Worker/SourceFileGuard.cs`
- Create: `src/NxDrawingPdfExporter.Worker/NxPartCloser.cs`
- Create: `tools/run-sheet-inventory.ps1`
- Create: `docs/verification/gate-2/sheet-inventory.schema.json`

- [ ] **Step 1: Add NX-independent source guard tests first**

Extend Core tests for a snapshot of full path, length, UTC mtime, and SHA-256. Product runtime compares length/mtime; validation scripts compare hashes. A detected change returns fatal failure.

- [ ] **Step 2: Open exactly one candidate without saving**

Use `Session.Parts.OpenDisplay(path, out PartLoadStatus)`. Capture all load-status messages before disposing it. On close, use `BasePart.Close(...DontCloseModified...)` and never invoke Save/SaveAs.

- [ ] **Step 3: Enumerate native collection order and map facts**

For every `workPart.DrawingSheets` item in returned order, record index, name, size/unit facts, and `GetDraftingViews().Length`. Keep the NX object array aligned to those indices.

- [ ] **Step 4: Run the inventory against the untouched private sample**

Before invocation, hash both PRT files. Run through `run_managed.exe`. Save only a privacy-scrubbed sheet inventory under `docs/verification/gate-2/`; generated raw logs remain gitignored under `artifacts/`.

- [ ] **Step 5: Compare with the visible NX navigator**

The developer must open the sample in NX and record that API order matches navigator top-to-bottom. If it does not, implement a narrow UF Drafting order adapter and repeat; alphabetical/natural name sorting is forbidden.

- [ ] **Step 6: Verify source hashes and gate result**

At the beginning of the validation run, compute a local baseline for both PRT files and retain it only in the gitignored run-artifact directory. Expected: the post-run hash for each file exactly equals that run's pre-run hash. Any mismatch is a hard stop; do not commit sample hashes or other private sample metadata.

- [ ] **Step 7: Commit**

```powershell
git add src/NxDrawingPdfExporter.Worker src/NxDrawingPdfExporter.Core tests tools/run-sheet-inventory.ps1 docs/verification/gate-2
git commit -m "feat: inspect NX drawing sheets safely"
```

### Task 7: Implement PDF validation and transactional publication

**Files:**
- Create: `src/NxDrawingPdfExporter.Core/Pdf/IPdfInspector.cs`
- Create: `src/NxDrawingPdfExporter.Core/Pdf/PdfInspection.cs`
- Create: `src/NxDrawingPdfExporter.Core/Pdf/PdfPageSize.cs`
- Create: `src/NxDrawingPdfExporter.Core/Output/IFileReplacer.cs`
- Create: `src/NxDrawingPdfExporter.Core/Output/SafeOutputPublisher.cs`
- Create: `src/NxDrawingPdfExporter.App/Pdf/PdfSharpInspector.cs`
- Test: `tests/NxDrawingPdfExporter.Core.Tests/SafeOutputPublisherTests.cs`
- Test: `tests/NxDrawingPdfExporter.App.Tests/PdfSharpInspectorTests.cs`

- [ ] **Step 1: Write failure-injection tests**

Test missing/zero-byte/non-PDF/wrong-page-count PDFs, successful new publish, successful overwrite, validation failure preserving old bytes, replacement exception restoring old bytes, and cleanup of only product-owned unique temp files.

- [ ] **Step 2: Confirm RED**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore --filter FullyQualifiedName~SafeOutputPublisherTests
```

- [ ] **Step 3: Implement managed inspection**

```csharp
public interface IPdfInspector
{
    PdfInspection Inspect(string path);
}

public sealed class PdfInspection
{
    public bool HasPdfHeader { get; set; }
    public int PageCount { get; set; }
    public long Length { get; set; }
    public IReadOnlyList<PdfPageSize> PageSizes { get; set; } = Array.Empty<PdfPageSize>();
}
```

`PdfSharpInspector` opens PDFsharp in read-only/import mode, checks `%PDF-` header, reads page count and MediaBox dimensions, then closes all handles. It runs in the App process after Worker output; Worker does not need PDFsharp.

- [ ] **Step 4: Implement safe publication**

Worker exports to `.<basename>.<runId>.<guid>.tmp.pdf` in the final target directory. App validates it, then:

- no old target: same-volume atomic move to final path;
- overwrite: `File.Replace(temp, target, backup)` when supported, verify final, delete backup only after success;
- failure: restore backup if needed, preserve original bytes, remove only known run-owned temp/backup paths.

- [ ] **Step 5: Run tests and commit**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore
& $taskDotnet test .\tests\NxDrawingPdfExporter.App.Tests\NxDrawingPdfExporter.App.Tests.csproj --no-restore
git add src tests
git commit -m "feat: validate and publish PDFs transactionally"
```

### Task 8: Export selected sheets through NX and complete sample PDF gate

**Files:**
- Create: `src/NxDrawingPdfExporter.Worker/NxViewUpdater.cs`
- Create: `src/NxDrawingPdfExporter.Worker/NxPdfExporter.cs`
- Create: `src/NxDrawingPdfExporter.Worker/NxFileJobRunner.cs`
- Create: `tools/run-sample-export.ps1`
- Create: `tools/render-verification-pdf.ps1`
- Create: `docs/verification/gate-2/README.md`

- [ ] **Step 1: Update only selected sheet views**

For each exportable sheet, call `sheet.Open()` and the locally verified `DraftingViewCollection.UpdateViews` overload. Any load/update exception fails that PRT and is not converted to `NoValidSheets`.

- [ ] **Step 2: Configure one multi-sheet NX PDF commit**

Use `workPart.PlotManager.CreatePrintPdfbuilder()`, `builder.SourceBuilder.SetSheets(selectedSheets.Cast<NXObject>().ToArray())`, and `builder.Filename = tempPath`. Set `builder.Colors = PrintPDFBuilder.Color.BlackOnWhite`, `builder.Size = PrintPDFBuilder.SizeOption.FullScale`, `builder.Watermark = string.Empty`, and `builder.Append = false`; do not set `XDimension`, `YDimension`, or a common scale. Call `Commit()` once and destroy builders in `finally`.

- [ ] **Step 3: Produce authoritative `FileResult`**

Record exported and skipped sheet names in navigator order, NX load/update diagnostics, temp path token, source guard outcome, and status. Exit code alone never means success.

- [ ] **Step 4: Run the private sample gate**

Hash both PRT files, export to gitignored `artifacts/gate-2/`, inspect the PDF through `PdfSharpInspector`, assert expected page count equals selected sheet count, and compare every page MediaBox to NX sheet dimensions.

- [ ] **Step 5: Render and visually inspect every page**

Rendering is validation-only and may use a pre-existing local PDF renderer after verifying its path/version; do not install one. Inspect black lines, white background, no watermark, cropping, orientation, title block, dimensions, and page order. Compare the A2 page with the private human reference PDF; do not require binary equality or expose its filename/content in committed evidence.

- [ ] **Step 6: Re-hash sources and document gate evidence**

The two expected hashes from Task 6 must remain identical. Commit the textual evidence and privacy-safe rendered contact sheet only if it exposes no confidential drawing content; otherwise keep renders ignored and record hashes/dimensions only.

- [ ] **Step 7: Commit**

```powershell
git add src tools docs/verification/gate-2
git commit -m "feat: export selected NX sheets to PDF"
```

### Task 9: Complete sequential batch orchestration, failure continuation, and cancellation

**Files:**
- Create: `src/NxDrawingPdfExporter.Core/Jobs/BatchStateMachine.cs`
- Create: `src/NxDrawingPdfExporter.Core/Jobs/ResultSummary.cs`
- Create: `src/NxDrawingPdfExporter.Worker/NxBatchRunner.cs`
- Create: `src/NxDrawingPdfExporter.Worker/CancellationFlag.cs`
- Test: `tests/NxDrawingPdfExporter.Core.Tests/BatchStateMachineTests.cs`
- Test: `tests/NxDrawingPdfExporter.Contracts.Tests/ResultSummaryTests.cs`

- [ ] **Step 1: Write failing state-transition tests**

Cover all eight statuses, single-file failure followed by next file, skip existing without Worker export, collision items never started, cancellation before first/next file, and cancellation delayed until safe publication finishes.

- [ ] **Step 2: Implement sequential loop only**

```csharp
foreach (var item in job.Items)
{
    if (cancellation.IsRequested) { appendCancelledRemainder(); break; }
    results.Add(ProcessOne(item));
    WriteAuthoritativeResultSnapshot();
}
```

No `Task.WhenAll`, `Parallel`, worker pool, or second NX session.

- [ ] **Step 3: Make result recovery durable**

Atomically rewrite `result.json` after each file. A crash leaves completed per-file results and a fatal worker record; the GUI reports incomplete, never success.

- [ ] **Step 4: Run tests and commit**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj --no-restore
& $taskDotnet test .\tests\NxDrawingPdfExporter.Contracts.Tests\NxDrawingPdfExporter.Contracts.Tests.csproj --no-restore
git add src tests
git commit -m "feat: orchestrate sequential NX batch jobs"
```

### Task 10: Build the WinForms GUI after the Worker gates pass

**Files:**
- Modify: `src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj`
- Create: `src/NxDrawingPdfExporter.App/Program.cs`
- Create: `src/NxDrawingPdfExporter.App/MainForm.cs`
- Create: `src/NxDrawingPdfExporter.App/MainForm.Designer.cs`
- Create: `src/NxDrawingPdfExporter.App/ApplicationController.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/NxInstallationDetector.cs`
- Create: `src/NxDrawingPdfExporter.App/Runtime/WorkerProcessLauncher.cs`
- Create: `src/NxDrawingPdfExporter.App/Logging/RunLog.cs`
- Create: `tests/NxDrawingPdfExporter.App.Tests/ApplicationControllerTests.cs`
- Create: `tests/NxDrawingPdfExporter.App.Tests/NxInstallationDetectorTests.cs`

- [ ] **Step 1: Write controller tests before controls**

Test default modes, mutual exclusivity, enable/disable rules, preflight blocking, progress transitions, cancellation, opening output/log paths, and summaries. Tests target controller/view interfaces, not pixel coordinates.

- [ ] **Step 2: Implement fail-closed NX detection**

Accept only the exact verified paths and `NXOpen.dll` file version `10.0.0.24`. Show detected root/version/status. Other NX versions are displayed as unsupported and cannot start.

- [ ] **Step 3: Implement process launcher**

Create a unique run folder under `%LOCALAPPDATA%\NxDrawingPdfExporter\runs\<runId>`, write `job.json`, invoke the gate-proven command, asynchronously capture stdout/stderr, read `result.json`, and map fatal/protocol failures to Chinese messages. Never put private full paths in UI telemetry; there is no network telemetry.

- [ ] **Step 4: Implement the approved single-window UI**

Controls: NX status, two input modes, recursive checkbox, file list, two output modes, unified directory, mutually exclusive skip/overwrite, preflight grid, start/cancel, current/total progress, per-file results, open output directory, open log. Defaults: scan current folder level, beside-source output, skip existing.

- [ ] **Step 5: Implement safe close**

While running, closing requests cancellation and waits for the Worker to finish the current safe operation; it does not kill the process during PDF replacement.

- [ ] **Step 6: Run tests/build and commit**

```powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.App.Tests\NxDrawingPdfExporter.App.Tests.csproj --no-restore
& $taskDotnet build .\NxDrawingPdfExporter.slnx -c Debug --no-restore
git add src/NxDrawingPdfExporter.App tests/NxDrawingPdfExporter.App.Tests NxDrawingPdfExporter.slnx
git commit -m "feat: add portable batch export GUI"
```

### Task 11: Prove all batch modes and failure paths on temporary fixtures

**Files:**
- Create: `tools/run-gate-3.ps1`
- Create: `docs/verification/gate-3/README.md`
- Modify: `README.md`

- [ ] **Step 1: Create disposable fixtures without touching originals**

Copy the private sample pair into a unique gitignored `artifacts/gate-3-fixtures/` directory. Create nested folders and controlled duplicate basenames there. Remove/corrupt dependencies only in copies.

- [ ] **Step 2: Execute the approved matrix**

Prove manual selection; current-folder and recursive scanning; beside-source and unified output; skip existing; successful safe overwrite; pure model skip; no-valid-sheet skip; missing dependency failure; unified-name conflict; one failure continuing to later items; cancellation before next file.

- [ ] **Step 3: Failure inject safe replacement**

Use `IFileReplacer` test doubles for deterministic unit failures and a disposable locked target for Windows integration. Verify original PDF SHA-256 after every failed overwrite.

- [ ] **Step 4: Verify no source or system mutation**

Re-hash original samples, confirm each post-run value equals the locally recorded pre-run value, ensure NX installation timestamps are untouched, and confirm no project code calls NX Save APIs using:

```powershell
rg -n "\.Save\(|SaveAs\(|SaveComponents|PartSave" src
```

Expected: no production save call.

- [ ] **Step 5: Document and commit gate 3**

```powershell
git add tools/run-gate-3.ps1 docs/verification/gate-3 README.md
git commit -m "test: verify NX batch export behaviors"
```

### Task 12: Package, inspect, and perform clean-extraction QA

**Files:**
- Create: `tools/publish-portable.ps1`
- Create: `tools/inspect-release.ps1`
- Create: `docs/verification/release/README.md`
- Create: `docs/verification/release/known-limitations.md`
- Modify: `README.md`

- [ ] **Step 1: Run the complete automated suite**

```powershell
& $taskDotnet test .\NxDrawingPdfExporter.slnx -c Release --no-restore
& $taskDotnet build .\NxDrawingPdfExporter.slnx -c Release --no-restore
```

Expected: zero failed tests and zero warnings.

- [ ] **Step 2: Publish the exact layout**

GUI: `win-x64`, self-contained, single file. Worker: normal `net48` x64 files under `worker/`. Package root contains `NX图纸批量导出工具.exe`, `worker/`, `THIRD-PARTY-NOTICES.txt`, and concise Chinese README only.

- [ ] **Step 3: Inspect forbidden contents**

`tools/inspect-release.ps1` fails if it finds `NXOpen*.dll`, `*.prt`, sample/reference PDFs, source files, logs, temp files, PDBs, secrets, or absolute developer paths. It emits file list and SHA-256 manifest.

- [ ] **Step 4: Clean-extraction and offline QA**

Extract/copy to a fresh directory outside the repo, disconnect network, launch without Python/Visual Studio/system modern .NET, and rerun one controlled sample export through installed NX. Verify outputs and source hashes.

- [ ] **Step 5: GUI visual QA**

Capture 100%, 125%, and 150% DPI screenshots. Inspect Chinese truncation, tab order, mode visibility, long paths, progress/results, disabled states, and no clipped buttons.

- [ ] **Step 6: Final static and placeholder audit**

```powershell
rg -n "TODO|TBD|NotImplementedException|throw new Exception\(\)|catch\s*\{\s*\}" src tests tools docs
git status --short
git log --oneline --decorate -15
```

Expected: no unresolved placeholders or empty catches; only intended verification artifacts are untracked/ignored; implementation commits are small and reviewable.

- [ ] **Step 7: Commit documentation only; do not push or release**

```powershell
git add README.md tools/publish-portable.ps1 tools/inspect-release.ps1 docs/verification/release
git commit -m "docs: record portable release verification"
git status --short
```

Expected: clean worktree. Stop and hand the local branch, evidence paths, release manifest, and known limitations to the user for independent review.

## Final Acceptance Evidence

The developer's final report must include:

1. Branch and ordered commit list.
2. Exact SDK/NX/.NET Framework versions actually used.
3. Test/build/publish commands with pass counts.
4. Gate 1 command, exit code, Session/UFSession proof.
5. Gate 2 sheet inventory, selected/skipped reasons, order proof, PDF page count/sizes, visual-QA location, before/after hashes.
6. Gate 3 behavior matrix and safe-overwrite recovery evidence.
7. GUI DPI screenshots and clean-extraction/offline result.
8. Release file manifest and SHA-256; proof of no Siemens/private files.
9. Known limitations and any user-authorized deviations.
10. Clean `git status`; no push and no release.
