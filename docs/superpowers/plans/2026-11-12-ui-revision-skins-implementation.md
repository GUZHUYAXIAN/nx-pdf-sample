# 界面、分组输出、升版与皮肤 Implementation Plan

> **For agentic workers:** 使用 `superpowers:executing-plans` 在新对话按任务顺序实施。本次交接推荐一个主执行代理持续集成，完成后独立审阅；不要为每个小步骤自动拆分多个写代码代理。若用户另选 subagent-driven，再使用 `superpowers:subagent-driven-development`，NX 会话仍禁止并行。按 `- [ ]` 记录实际结果。

**Goal:** 在 v2.0.1 双 NX 基线上交付方案 A 的完整界面、分组目录映射、可编辑升版、双语及四套离线角色皮肤。

**Architecture:** 保留 WinForms GUI/Core/Contracts/双 Worker 分层。Core 生成可解释输出计划与严格发布意图；App 保存会话编辑、冻结计划并使用标准表格与局部自绘展示；Worker 只串行输出临时 PDF。

**Tech Stack:** 现有 C#、.NET SDK 10.0.400、net10.0-windows WinForms、netstandard2.0 Core/Contracts、net48 x64 Worker、MSTest、PDFsharp、System.Drawing 与仓库现有 PowerShell 工具；不安装新依赖。

**Spec:** `E:\Codex\projects\nx-pdf-sample\worktrees\v2\docs\superpowers\specs\2026-11-12-ui-revision-skins-design.md`

**Status:** 依据用户已确认交互和方案 A 编写，独立设计审阅的工程补充已纳入；尚未开始任何实施或测试。用户要求审阅无实质冲突后交接到新对话执行。

## Global Constraints

- 活动目录仅 `E:\Codex\projects\nx-pdf-sample\worktrees\v2`；外层为旧 V1，不覆盖；相邻 nx-step-launcher 只读。
- Windows 11 x64；WinForms GUI 不加载 NXOpen；两代 Worker 与已验证组件元组保持；NX 串行、不保存 PRT。
- 版次范围 0–2147483647；0 无后缀，正整数追加 `-R数字`；推荐来自源/实际目标两目录的数值最大版次 + 1。
- 目录、版次、运行中变化、别名及同家族冲突严格按设计 §§4–7，不以静默改名/覆盖规避。
- 默认分组输出、跳过原名 PDF、简体中文、云白；5 纯色+4 真实角色皮肤完整交付。
- 所有业务路径/手动版次仅本会话；UI 设置独立保存，不能破坏 NX 配置。
- 产品运行离线。既有 SDK/缓存外的安装或下载需另行授权；开发期 imagegen 素材制作不等于产品运行联网。
- 不触碰私有源样例；故障/取消/覆盖测试用新建隔离副本或合成文件；每次真实 NX 前后核对两份 PRT SHA-256。
- GUI/DPI/物理断网由用户按清单操作；自动化测试不能冒充其通过证据。
- 小步本地提交，只暂存本任务准确路径；不推送、打标签、创建 Release；未知工作区文件保留。

## Review Focus

1. 不同源目录的同名 PRT 指向同一输出家族，即使人工选不同版次也应报冲突：Task 3。
2. 预检查后插入同名 PDF，新增不能变成覆盖；覆盖目标变更不能假成功：Task 4、5。
3. 正在编辑版次时切语言/换皮肤/点击开始，编辑值不能丢失或被自动推荐替换：Task 5、6、7。
4. 目录大小写、不同父目录同叶名称、超过 26 组、筛选隐藏行和手动清单折叠不能改变身份/任务范围：Task 2、5、7。
5. 中文长文件名、150% DPI、坏图/超大图/包内图片缺失或配置损坏，不能使导出界面失去可用性或误报皮肤完成：Task 6、8、9。

## 启动检查与验证命令

先读本工作树 AGENTS、README、两份旧正式设计、本次设计、本计划和交接提示词；旧文件中与本次增量冲突的条目以本次用户批准规则为准。记录 `git status --short --branch`、`git log -1 --oneline`、`git diff --stat`，保留所有无关改动。

已有 V2 是隔离工作树，无需复制整个仓库或创建根目录之外的工作树。可在该工作树创建 `codex/ui-revision-skins` 本地分支；若分支已存在先核对，不重置或覆盖。分支/提交可追溯设计、计划和执行状态。

已只读确认 SDK 路径存在且有 10.0.400 目录。执行者仍核对当前实际版本；仅设置子进程环境，不写全局变量：

```powershell
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
& $taskDotnet --version
& $taskDotnet test .\NxDrawingPdfExporter.slnx -c Release --no-restore
```

在此记录真实基线，不复述历史 223 项作为本次通过。若 assets 指向失效目录，只允许按 README 用已存在缓存作为唯一源离线还原，禁止网络 restore。SDK/缓存缺失则报告，不安装。

下面用 C、A、P、W 指测试项目的路径简写，只用于本文描述：

- C = `tests/NxDrawingPdfExporter.Core.Tests/NxDrawingPdfExporter.Core.Tests.csproj`
- A = `tests/NxDrawingPdfExporter.App.Tests/NxDrawingPdfExporter.App.Tests.csproj`
- P = `tests/NxDrawingPdfExporter.Contracts.Tests/NxDrawingPdfExporter.Contracts.Tests.csproj`
- W = `tests/NxDrawingPdfExporter.Worker.Tests/NxDrawingPdfExporter.Worker.Tests.csproj`

每个逻辑任务：先写以下指定断言，运行对应项目确认新断言确实失败，再实现，再运行同一测试项目到退出码 0。命令是 `& $taskDotnet test <实际项目路径> -c Release --no-restore`，不得把 C/A/P/W 当文件名运行。任务提交前运行 `git diff --check`、审阅本任务差异。纯视觉间距/颜色不写镜像测试，用人工视觉检查；模型、状态和文件逻辑有真正反例测试。

## 文件结构和共享类型

新 Core 类型使用 netstandard2.0 可用 API 与普通不可变类，不引入仅 net10 支持的运行时 API。下面字段为对后续任务的接口约定，具体构造函数和内部实现可由执行者选择。

| 文件/区域 | 职责 |
|---|---|
| `Core/Input/SourceDirectoryGrouper.cs` | 目录归组与稳定组标签 |
| `Core/Output/OutputRoutingSettings.cs` | 两模式及每组目标解析 |
| `Core/Output/RevisionNumber.cs`、`RevisionFileName.cs` | 输入解析、精确文件名解析和构造 |
| `Core/Output/RevisionCatalog.cs` | 仅两目录的家族枚举与事实 |
| `Core/Output/RevisionOutputPlanner.cs`、`OutputPlanningModels.cs` | 新的完整规划接口，先与旧 planner 并存以保持中间构建 |
| `Core/Output/TargetSnapshot.cs`、`ITargetStateReader.cs` | 目标存在性、指纹及变化检测 |
| `Core/Output/SafeOutputPublisher.cs`、`IFileReplacer.cs` | 有意图的新增/覆盖、备份和冲突结果 |
| `App/Session/PreflightSession.cs` | 手动编辑、失效、刷新、运行冻结 |
| `App/Localization/` | 资源和状态原因本地化 |
| `App/Configuration/UiSettings.cs`、`JsonUiSettingsStore.cs` | 与 NX 设置隔离的 UI 持久化 |
| `App/Controls/`、`App/Dialogs/`、`App/Theming/` | 标准表格封装、详情、皮肤预览与绘制 |
| `App/Assets/Themes/` | 受控四套素材及参数，发布复制为 `assets/themes/` |

上表中的 Core/App 是 `src/NxDrawingPdfExporter.Core` 和 `src/NxDrawingPdfExporter.App` 的简写。

共享模型：

- `SourceDirectoryGroup`：`Key`（规范化完整父目录）、`Label`（A/AA）、`SourcePaths`（有序列表）。颜色由 UI 根据组标识/主题生成，不放进业务模型。
- `OutputRoutingSettings`：`Mode`、`UnifiedDirectory`、`GroupTargets`（规范化父目录→目标目录）。
- `RevisionChoice`：`Value`（int）、`IsManual`（bool）；UI 另保存无效编辑文本和错误，不能把无效输入当 0。
- `RevisionNameMatch`：`IsMatch`、`Revision`（int?）、`IsCanonical`、`IssueCode`（string?）。
- `RevisionCatalogSnapshot`：源/目标目录、各自最高版次、家族文件事实、读取问题、用于复核的事实摘要。
- `TargetSnapshot`：是否存在、可比较的身份/长度/写入时间/内容 SHA-256；不存在也是明确状态，不能用 null 同时代表读失败。
- `PlannedPublishIntent`：`None`、`CreateNew`、`ReplaceExpected`；None 用于跳过/问题项。
- `OutputPlanningRequest`：有序源文件、Routing、Policy、ManualChoices；字典按规范化源路径匹配。
- `PlannedOutputItem`：SourcePath、SourceOrdinal、GroupKey、FinalOutputPath、SuggestedRevision、ChosenRevision、IsManual、Status、ReasonCode/ReasonArguments、CatalogSnapshot、PublishIntent、ExpectedTarget。
- `OutputPlan`：有序 Items、HasBlockingIssues、输入上下文标识。`FrozenExportBatch` 位于 App/Session（不放 Core），为深拷贝且不可变的有序条目及所选 `NxInstallation` 快照，不产生 Core→App 依赖。

## Task 1：协议 2 与双 Worker 的明确语义

**Files:** 修改 `src/NxDrawingPdfExporter.Contracts/{Enums.cs,ProtocolVersion.cs,JobJsonSerializer.cs,FileResult.cs}`，新建同目录 ResultReasons.cs；按影响修改 `Core/Jobs/BatchStateMachine.cs` 和两代 Worker 共享源码中的结果构造；测试 P 中 `JobJsonSerializerTests.cs`、C 中 `BatchStateMachineTests.cs`、W 中现有匹配/快照测试。

**Interfaces:** `ProtocolVersion.Current = "2"`；保留旧枚举的数值，新增 `OutputMode.GroupedDirectories = 2` 与 `ExistingPdfPolicy.Revision = 2`。`JobItem` 仍传最终绝对路径，Worker 不推算版次或正式发布。

`FileResult` 新增 `ReasonCode: string`、`ReasonArguments: string[]`、`TechnicalDetails: string`。ResultReasons 定义稳定产品原因常量，覆盖现有结果分支和新 TargetChanged；旧 Message 仅诊断兼容，不用于业务判断或当前语言渲染。Core 发布结果也提供相同结构的原因数据；未知原因用通用本地化说明+原始技术详情。

- [ ] 添加断言：协议 2 及 ReasonCode/ReasonArguments/TechnicalDetails 往返、协议 1/999 拒绝、未知枚举拒绝；GroupedDirectories 接受逐项不同输出目录；Revision 正常处理已有正式目标的临时导出，仍串行；GUI 已剔除预检跳过项，Worker 收到的 Skip 项若目标突然出现应 Failed/TargetChanged，不能返回 SkippedExisting；模型缺失/更新失败有不同原因代码且原文保留。
- [ ] 运行 P、C、W，确认新增行为失败；保留真实 RED 输出摘要。
- [ ] 实现枚举/版本及严格验证，更新项目拥有的固定版本测试数据，核对两代 Worker 共享同一 Contracts；修订 BatchStateMachine 旧的运行时跳过分支。不能让旧 Worker 吃下新任务。
- [ ] 运行 P、C、W；完整 Release 构建退出码 0，未知字段/坏 result/RunId 防护不退化。
- [ ] 只提交任务路径：`feat: version grouped and revision job contracts`。

## Task 2：目录分组与独立目标映射

**Files:** 新建 Core/Input/SourceDirectoryGrouper.cs、Core/Output/OutputRoutingSettings.cs；测试新建 `SourceDirectoryGrouperTests.cs`、`OutputRoutingTests.cs`（C）。

**Interfaces:** `SourceDirectoryGrouper.Group(IReadOnlyList<string> sources, IReadOnlyDictionary<string,string> knownLabels) -> IReadOnlyList<SourceDirectoryGroup>`；`OutputRoutingSettings.ResolveDirectory(string sourcePath) -> string`。Task 1 的 GroupedDirectories 表示分组模式，旧 BesideSource 只作旧代码过渡，最终 UI 不使用它冒充分组。

- [ ] 先写断言：同完整目录归一、大小写/尾斜杠归一、不同父目录同叶名分开、同源文件去重保序、26 后为 AA；单组改目的不影响其他组；统一/分组来回保留字典。
- [ ] 运行 C 证实 RED。
- [ ] 实现仅依赖路径/数据的分组与解析；组标签复用会话已有标签，目标目录错误返回可解释错误而非源目录静默兜底。
- [ ] 运行 C 为 GREEN；检查所有例子使用合成路径，不扫描私有样例。
- [ ] 提交：`feat: route exports by source directory group`。

## Task 3：版次解析、两目录历史与完整预检查计划

**Files:** 新建 Core/Output/RevisionNumber.cs、RevisionFileName.cs、RevisionCatalog.cs、RevisionOutputPlanner.cs、OutputPlanningModels.cs、TargetSnapshot.cs、ITargetStateReader.cs；新测试 C/RevisionNumberTests.cs、RevisionCatalogTests.cs、RevisionOutputPlannerTests.cs。可拆出 PhysicalOutputDirectoryReader/PhysicalTargetStateReader 文件。

**Interfaces:**

- `RevisionNumber.TryParse(string text, out int value) -> bool`。
- `RevisionFileName.Parse(string sourceBaseName, string fileName) -> RevisionNameMatch`；`Format(string sourcePath, int revision) -> string`（文件名）。
- `IOutputDirectoryReader.ReadFiles(string directory) -> IReadOnlyList<string>`（完整路径，失败抛出可分类异常）。
- `ITargetStateReader.Capture(string path) -> TargetSnapshot`（失败不得伪装不存在）。
- `RevisionCatalog.Read(string sourcePath, string targetDirectory) -> RevisionCatalogSnapshot`。
- `IRevisionOutputPlanner.Plan(OutputPlanningRequest request) -> OutputPlan`；生产实现 `RevisionOutputPlanner`，暂不删旧 OutputPlanner。

- [ ] 添加关键断言：`TryParse("r100") => 100`，`TryParse("R0") => 0`，负数/小数/路径/空值/溢出 false；`Format("组件-R2.prt",1) == "组件-R2-R1.pdf"`。
- [ ] 添加 catalog/planner 断言：无历史0；源R2空目标3；源R2目标R5推荐6；R100>R9、修改时间无关；两处相同只读一次；无关/备份/临时/相似前缀不匹配；不可读取与异常别名阻塞；max int 不溢出。
- [ ] 添加手动与冲突断言：输入2覆盖仅实际目标R2；只源有R2时目标新增；手动0原名；非升版策略只看原名；同目标文件及不同源同目标家族（即使版次不同）都阻塞；升版时本批 A.prt 与 A-R1.prt 在重叠查找范围/目标下的家族歧义阻塞，只有后者时命名正常；非升版两个不同原名可用；原因包含两处最高值和手动选择依据。
- [ ] 运行 C 证实 RED，实现上述接口；为指定目标读取指纹，枚举不打开 NX、不写目录。
- [ ] 运行 C GREEN，检查结构化原因没有硬编码 UI 语言或误报图纸已验证。
- [ ] 提交：`feat: plan editable numeric drawing revisions`。

## Task 4：受冻结意图约束的安全发布

**Files:** 修改 Core/Output/SafeOutputPublisher.cs、IFileReplacer.cs；使用 Task 3 的 TargetSnapshot；修改/扩展 C/SafeOutputPublisherTests.cs，必要时增加 `PublicationRaceTests.cs`。

**Interfaces:** `PublicationRequest` 新增 `Intent: PlannedPublishIntent` 和 `ExpectedTarget: TargetSnapshot`；注入 `ITargetStateReader`；结果增加明确 `TargetChanged` 或等价失败原因。旧调用者先显式构造意图/快照保持编译，Task 5 再接入预检查捕获的真正快照，最终不能在 Publish 内临时批准覆盖。

- [ ] 用可控文件操作替身制造：规划新增后目标出现；规划覆盖后内容变化（长度相同也要发现）；预检查后文件消失；共享占用；临时 PDF 损坏；替换失败；复验失败；备份清理失败。
- [ ] 断言：新增永不调用替换；变化返回错误且第三方内容保留；成功覆盖只在临时验证后发生；恢复不能覆盖后来出现的未知文件；只清理当前拥有的临时路径；备份未安全处置时保留并报告。
- [ ] 运行 C 证实 RED，实现意图分支、指纹检查和可靠备份/恢复。对 File.Replace 的竞争窗口，比较实际备份和期望身份，不把“发布前检查一次”当作无竞争保证；异常时以保留新旧证据为先。不能编写无条件覆盖第三方新文件的恢复逻辑。
- [ ] 在本项目 artifacts 下新目录用合成 PDF 做真实本地文件系统验证；不使用原私有 PDF。测试原子移动碰撞和占用失败，记录实际边界，不声称网络盘事务隔离。
- [ ] 运行 C GREEN；提交：`fix: honor planned publication intent and target changes`。

## Task 5：会话编辑、计划冻结与控制器端到端接入

**Files:** 新建 App/Session/PreflightSession.cs、FrozenExportBatch.cs；修改 App/ApplicationController.cs、MainForm.cs 的现有绑定以保持中间可用；必要时移除 Core/Output/OutputPlanner.cs 的旧入口；新增 A/PreflightSessionTests.cs，扩展 ApplicationControllerTests.cs。

**Interfaces:** `PreflightSession` 持有 Inputs、Routing、Policy、ManualChoices、CurrentPlan、HasInvalidEdit、IsStale；`SetRevision(string sourcePath,string text) -> bool`、`RestoreSuggested(IReadOnlyList<string> selectedSources)`、`Refresh() -> OutputPlan`、`FreezeForRun(NxInstallation selectedNx) -> FrozenExportBatch`。NxInstallation 来自 App 既有检测模型，不是 NXOpen 对象。Freeze 提交编辑并对 Task 3 的相关家族和目标快照重新验证。RunCore 只消费冻结批次；Worker 返回后逐项发布前再次比较相关 CatalogSnapshot，随后交给 Task 4 复核最终目标。

- [ ] 添加断言：手动R2跨刷新/重复运行保留；目录变化保留手动值但 IsStale；切策略往返可恢复；来源移除不污染其他源；筛选不改变 Inputs；无效编辑无法冻结。
- [ ] 添加控制器断言：手动R2确实进入 job 的目标R2；不在开始时重算R3；选定NX快照和顺序保留；开始前变化不启动 Worker；运行中变化失败该项且不悄改版；最终目标仍不存在但另一历史R版在 Worker 运行期间新增/删除/变成别名时不发布；无关家族/自有临时文件不触发；只按冻结意图调用发布；全部跳过不启动 NX。
- [ ] 运行 A/C 证实 RED；替换现有 Preflight()/RunCore() 的独立重算路径，深拷贝冻结数据，结果对回原序号/组；移除最终未使用的过渡旧 planner 调用。
- [ ] 运行 A/C/P/W GREEN；保留纯模型、无有效页、坏结果、缺依赖、单文件失败继续和取消回归。
- [ ] 提交：`feat: preserve revision edits through frozen export runs`。

## Task 6：本地化与独立 UI 设置

**Files:** 新建 App/Localization/UiText.cs、Strings.resx、Strings.en.resx；新建 App/Configuration/UiSettings.cs、JsonUiSettingsStore.cs；新建 A/LocalizationTests.cs、UiSettingsStoreTests.cs；逐步替换 MainForm/ApplicationController/摘要的用户文字。

**Interfaces:** `IUiText.Get(string key, params object[] arguments) -> string`，语言切换事件；UiSettings 字段固定为 SchemaVersion、Language、ThemeId、ThemeParametersById、FontScale、WindowBounds、SplitterRatio、ColumnWidths。不含输入/输出业务路径；图片引用只指应用自有导入副本。

- [ ] 写 RED：中文默认、英文键完整且可格式化；Task 1 的全部产品 ReasonCode 可渲染，未知代码有本地化兜底和技术详情；完成后切语言重新渲染结果，不解析中文 Message；切换语言不改计划/手动值；损坏JSON/未知schema/越界布局/缺主题回退且NX设置字节不变；写入失败旧配置保留。
- [ ] 运行 A 证实 RED；实现独立 ui-settings.json、同目录临时写入、安全替换；保留原 NxSettingsStore schema 1。异常给稳定说明，不吞掉保存失败。
- [ ] 运行 A GREEN；检查产品拥有的字符串覆盖菜单、对话框、预检、错误和汇总，NX原文仅留详情。
- [ ] 提交：`feat: localize the interface and persist appearance settings`。

## Task 7：方案 A 的窗口、目录映射与可编辑表格

**Files:** 修改 App/MainForm.Designer.cs、MainForm.cs；新建 App/Controls/PreflightGrid.cs、DirectoryOutputPanel.cs、CollapsibleSection.cs；新建 App/Dialogs/FileDetailsDialog.cs；扩展 A/MainFormStateTests.cs，必要时新建 UI 状态投影测试。

**Interfaces:** 绑定 Task 5 的 Session 与 Task 6 的 UiText/UiSettings；表格行只读投影 PlannedOutputItem，版次编辑调用 SetRevision；目录面板修改 Routing 并置陈旧。不要在绘制事件读取文件系统或运行预检查。

- [ ] 先为逻辑绑定写 RED：展开不切换模式；扫描下手动清单只读；浏览修改单组不切统一；统一行中性并只一条；切回恢复映射；双击版次编辑/其他列详情分流；搜索/筛选不改运行范围；选中项目录不是第一行目录。
- [ ] 运行 A 证实 RED，实现 MenuStrip、上方可收起设置、中央水平 SplitContainer、底部固定运行控件、标准 DataGridView 和详情。实现序号/组/源名/目标名/状态/版次顺序及列显示条件。
- [ ] 文件名换行且可调宽，完整路径 tooltip，状态原因 tooltip，组浅底纹+标识；多于26组、选中态、深色和高对比度可识别。标准键盘编辑和 Tab 顺序保留。
- [ ] 为“点开始”和“切语言/主题”统一提交/保留编辑行为；无效编辑就地报错而非自动清零。新增批量设置选中行与恢复推荐入口。
- [ ] 运行 A GREEN，并生成用户人工清单：100/125/150% DPI、窄窗/长文件名、拖分隔、折叠、键盘、同色映射和菜单。没有实际截图先标待验。
- [ ] 提交：`feat: redesign the grouped export workspace`。

## Task 8：纯色、四套角色皮肤与图片调整

**Files:** 新建 App/Theming/ThemeDefinition.cs、ThemeCatalog.cs、BackgroundRenderer.cs、ImportedImageStore.cs；App/Dialogs/AppearanceDialog.cs；App/Assets/Themes/ 下的 rem/inori/frieren/roxy 原图/缩略图/参数和素材来源清单；新增 A/ThemeSettingsTests.cs、ImageImportTests.cs、ThemeCatalogTests.cs；按需新建 .resx 资源。

**Interfaces:** `ThemeCatalog.Load(string assetsRoot) -> IReadOnlyList<ThemeDefinition>`；`BackgroundRenderer.Render(Bitmap source, Size target, ThemeParameters parameters) -> Bitmap`；参数包含 ImageVisibility、Blur、MistOpacity、GradientStrength、Zoom、OffsetX/Y，0–100 映射符合设计；`ImportedImageStore.Import(string inputPath) -> string` 返回应用私有副本引用。

- [ ] 写 RED：五纯色+四角色ID；预览取消回滚/应用持久化/恢复只当前主题；换主题保留组标签和手动版次；坏图/32MiB超限/4096万像素超限/丢失图有明确回退；原图不改且释放句柄；renderer 的零效果和不同窗口比例行为。
- [ ] 运行 A 证实 RED，实现配色/缩放/雾化/模糊/渐变与有界缓存；只对背景处理，表格和字保持清晰。异步计算使用请求序号或取消处理，旧预览结果不能覆盖最新滑块状态。
- [ ] 读取 imagegen 技能，使用已提供图像工具制作四张真实角色插画，不写产品 API/凭据逻辑；统一横向构图。调用前检查工具返回的保存机制，最终素材必须落在本工作树受控 Assets 下并可解压携带。用户未提供具体画面，按设计者构图默认值制作并如实标注为生成式插画。
- [ ] 检查每张实际图：角色身份、脸/手、清晰度、裁切、工作区留白和整套一致性；记录来源。工具不可用时报告素材缺口，可继续独立逻辑但不能把占位当完成。
- [ ] 运行 A GREEN；让用户实际查看四套皮肤和效果组合，未验保持待验；提交产品素材和代码，不提交用户导入图或缓存。
- [ ] 提交：`feat: add four offline character skins and image controls`。

## Task 9：集成、候选包与分层验收

**Files:** 修改 App/NxDrawingPdfExporter.App.csproj、tools/publish-portable.ps1、tools/inspect-release.ps1 及其已有 test-*.ps1；README.md、docs/verification/release/known-limitations.md；新建 docs/verification/ui-revision-skins/{README.md,manual-acceptance.md}；Directory.Build.props 更新 Version=2.1.0、AssemblyVersion/FileVersion=2.1.0.0，候选说明明确本地待验，未获发布授权不更改远端。

**Interfaces:** 包结构保留 EXE、worker/nx10、worker/nx2506，新增 assets/themes 相对路径。发布脚本从受控素材复制，manifest 覆盖每张图、缩略图、参数和声明。App 以 AppContext.BaseDirectory 解析包内资源，不依赖工作目录。

- [ ] 先扩展包检查反例：缺任一角色原图/参数、错误相对路径、丢失英文资源、混用协议1Worker、加入用户导入图、Siemens DLL/私有文件均拒绝；正常完整包通过。
- [ ] 按现有测试脚本先见 RED，再实现打包/审计；素材EXIF/文本元数据不泄露开发机绝对路径，用户图不打包。原图、缩略图、参数、来源清单必须先成为受控文件并进入 source-manifest.sha256；本计划放在 src 下可沿用现有源码范围，若改到根 assets 必须同步扩展范围。脚本只处理全新 release 目录。
- [ ] 全量 `dotnet test NxDrawingPdfExporter.slnx -c Release --no-restore` 与 `dotnet build ... -c Release --no-restore` 退出码0；运行现有包脚本测试。只对失败/新改动复测，不无意义循环全量。
- [ ] 独立审阅代码、规范覆盖、发布/版本错误路径，修复实质问题后重跑受影响测试。审阅者可只读，NX 不并行。
- [ ] 在项目内新 artifacts 目录、使用完整关联模型的隔离副本，分别顺序验证两代真实 Session/导出/0版/新R版/指定旧R版/分组目标/取消；每次 before/after SHA-256 对应，PDF 逐页渲染检查。不得只用测试替身宣称真机通过。
- [ ] 生成一个全新的本地候选包，例如 `artifacts/release/ui-revision-skins-candidate-日期`，SHA-256清单自检，并在项目范围内另一全新目录干净解压核对。跨项目人工解压、DPI/离线操作由用户按既有流程执行。
- [ ] 提交文档中逐项写真实状态：自动化/构建/真实NX/PDF视觉/GUI与DPI/物理断网/其他机器/四套素材。未做或工具受限写待验，说明候选包位置和可执行人工步骤。
- [ ] 提交：`test: package and document the revised desktop exporter`；最终不 push/tag/release。

## 自审与交接检查

- [x] 设计 §§3–5 → Task 2、5、7；§6 → Task 3、4、5；§7 → Task 1、4、5；§8、10 → Task 6、7；§9 → Task 8、9；§12 → Task 9。
- [x] 核对计划内所有任务使用相同的 RevisionChoice、OutputPlan、TargetSnapshot、FrozenExportBatch 和发布意图，明确防止 GUI 编辑值留在未使用的旁路数据中。
- [x] 核对“旧策略仍作用原名”“目录改动不切模式”“源+目标最大版次”“搜索不改任务范围”“同版重导保留值”均有对应测试要求与界面入口要求（尚未实现）。
- [x] 设计/计划审阅与功能验证分开：本阶段文档审阅不能登记任何产品测试或真机 PASS。
- [x] 最终交接提示词包含实际绝对目录、必读设计与计划、明确实施授权、可自主处理细节、需要用户介入的真实条件和不得扩大范围的边界。
