# 分阶段实时导出进度 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: 使用 superpowers:executing-plans 逐项实施，一个主执行者集成；允许最后独立只读审阅。先完成同日 adaptive-group-colors 计划，按 - [ ] 记录实际完成，不并行NX或发布。

**Goal:** 用户能看到真实的当前文件/子步骤、已处理/已生成/已保存数量和同条双色阶段进度，取消有即时反馈。

**Architecture:** 增加仅展示用途的本地原子累计快照progress.json；Worker顺序报告，GUI在执行期间监视。App状态机分导出与校验保存，原最终result.json校验和正式发布仍是唯一业务依据。

**Tech Stack:** netstandard2.0共享Contracts/Core、两代net48 Worker、net10.0-windows App、已有MSTest和System.Drawing；无新依赖。

**Spec:** E:\Codex\projects\nx-pdf-sample\worktrees\v2\docs\superpowers\specs\2026-11-13-adaptive-groups-phase-progress-design.md

## Global Constraints

- 活动工作树、基线和现有环境同分组色计划；完整读AGENTS/README及所有批准设计。
- job/result协议仍为2，旁路进度schema1；不得用进度快照发布任何PDF、改变路径或跳过结果验证。
- 两代Worker共用进度实现；GUI不加载NXOpen，源PRT不保存，串行NX和顺序发布。
- 导出分母为实际Worker条目；发布分母为可信最终结果中的可发布临时成功项。失败计入已处理，不计成功；取消未执行不填充。
- 一条进度条：已处理比例、一个当前条目比例、剩余底色。文件内部无可靠百分比就不伪造。
- 读取显示故障不能使导出失败或假成功；原权威结果快照写入失败仍按原致命恢复逻辑处理，不吞掉。
- 已有源/模型保护、版次与CatalogSnapshot/目标身份/临时PDF验证保留。
- 本轮改了Worker观察路径，需两代真实受控验证；逐次源/模型SHA-256，私有原件不改、不提交。
- 既有SDK/缓存，--no-restore；用户做GUI/DPI/物理断网；本地候选2.1.0新唯一目录，不推送/发布。

## Review Focus

1. 取消后AppendCancelledRemainder或FatalResultMerger补齐列表使Files.Length看似100%：Task2、4。
2. 未读到中间快照、旧runId/sequence、同一表面读写竞争或畸形计数：Task1、3。
3. 第一次取消发生在cancel.flag创建之前，或迟到导出消息覆盖“取消/阶段2”：Task3、4。
4. 安全发布同步阻塞UI，阶段2数值更新却无法绘制；后台工作误用可变Session：Task4。
5. 进度成功但最终result未完成/身份错误/目标发生竞态：Task4、6，仍拒绝发布且保留旧PDF。

## 路径与运行命令

路径相对活动V2。任务使用相应App/Contracts/Core/Worker测试项目，在本次PowerShell进程定义实际路径：

~~~powershell
$testProjects = @{
    App = '.\tests\NxDrawingPdfExporter.App.Tests\NxDrawingPdfExporter.App.Tests.csproj'
    Contracts = '.\tests\NxDrawingPdfExporter.Contracts.Tests\NxDrawingPdfExporter.Contracts.Tests.csproj'
    Core = '.\tests\NxDrawingPdfExporter.Core.Tests\NxDrawingPdfExporter.Core.Tests.csproj'
    Worker = '.\tests\NxDrawingPdfExporter.Worker.Tests\NxDrawingPdfExporter.Worker.Tests.csproj'
}
& $taskDotnet test $testProjects.Contracts -c Release --no-restore
~~~

$taskDotnet及子进程缓存环境沿用上一计划。上例为Task1；Task2分别使用$testProjects.Core、$testProjects.Worker及$testProjects.Contracts，Task3–5使用$testProjects.App。先失败测试、确认失败、实现、通过并审阅git diff --check后小步提交。最终发布脚本做同一源码的全量检查，无新问题不重复全套验证。

## Task 1：独立进度DTO和读取校验

**Files:** 新增 src/NxDrawingPdfExporter.Contracts/WorkerProgressSnapshot.cs、WorkerProgressJson.cs；新增 tests/NxDrawingPdfExporter.Contracts.Tests/WorkerProgressTests.cs。

**Interfaces（共享类型使用netstandard2.0兼容普通类，不引入App/NX引用）:**
- WorkerProgressStage：Initializing、Ready、CheckingItem、Opening、Inspecting、Updating、GeneratingPdf、Closing、BetweenItems、Finished、Faulted。
- WorkerProgressSnapshot：int SchemaVersion=1；string RunId；long Sequence；DateTime UpdatedUtc；WorkerProgressStage Stage；int Total/Processed/Generated/Failed/PureModel/NoValidSheets/OtherProcessed；int? CurrentItemIndex。
- WorkerProgressJson.Serialize(snapshot)、Deserialize(json)、WriteAtomic(snapshot,path)、ReadBounded(path,maximumBytes=131072)、ValidateForRun(snapshot,runId,total,lastSequence)。
- 原JobRequest/JobResult/ProtocolVersion及JobJsonSerializer业务校验不改。旁路固定为job.json同目录的progress.json，不接受快照指定新路径。

- [ ] 写往返/缺必需字段/未知schema枚举/空runId/负数/越界/计数不等于分类和的失败测试；Processed≤Total，Generated等各计数非负，分类和=Processed；活动索引在0..N-1且等于当前顺序Processed，未活动状态必须为空。
  执行记录：测试覆盖往返、缺字段、未知枚举、错误计数、错误索引和运行身份。各负数、未知 schema 和空 runId 由校验器直接拒绝。
- [x] 验证runId及N匹配；seq必须增加，可跳号但不能重复/倒退；UpdatedUtc仅辅助诊断，不用时钟顺序代替seq。
- [x] 大于128KiB、非法JSON和原子写竞争读取测试；读句柄允许原子替换，不阻塞Worker写入。
  执行记录：覆盖大小限制和并发替换期间读取恢复，并保留旧读句柄验证。
- [x] 实现独立序列化与同目录临时写/替换，失败不破坏旧快照，临时文件仅属于本次写入。通过Contracts测试，本地提交：feat: define advisory worker progress snapshots。

## Task 2：真实文件/子步骤报告与累计计数

**Files:** 新增 src/NxDrawingPdfExporter.Core/Jobs/BatchItemProgress.cs；修改同目录BatchStateMachine.cs；新增 src/NxDrawingPdfExporter.Worker/WorkerProgressReporter.cs；修改Program.cs、NxBatchRunner.cs、NxFileJobRunner.cs，必要时NxViewUpdater.cs；扩展Core BatchStateMachineTests及Worker RunJobFatalSnapshotTests，新增WorkerProgressReporterTests。

**Interfaces:**
- BatchItemProgress：int ItemIndex、bool Completed、FileResult? Result；Run(...) 末尾新增可空Action<BatchItemProgress> observer，既有调用保持兼容。
- IWorkerProgressReporter.Step(WorkerProgressStage stage,int? itemIndex)、CompleteItem(int index,FileResult result)、Finish(bool faulted)；生产由WorkerProgressReporter维护完整累计快照，提供无操作实现给非batch入口。
- NxBatchRunner接收可选reporter；NxFileJobRunner.ExportSingle追加可选子步骤回调Action<WorkerProgressStage>，既有--export调用保持兼容。
- reporter只管理展示文件；所有观察入口隔离预期观察IO异常并输出一次诊断，不能吞掉processor/source guard/原result快照的异常。

- [x] 写事件顺序测试：Start→实际Process返回（含finally关闭/源检查）→原result快照成功→Completed；首文件完成后第二文件活动，分类累计正确。
- [x] 写取消/失败反例：取消余项不产生Completed，失败/纯模型/无有效图纸各增加Processed但不增加Generated；致命合并补出的未执行失败不计进度。
- [x] 写独立故障测试：progress写失败不改变原输出结果；原result快照失败仍触发现有致命恢复，不能被进度异常处理捕获而继续假成功。
- [x] 接入Program在有效job读入后的初始化；NxRuntimeGuard真实通过后Ready；在真实NX调用前报告打开/检查页/更新/生成/关闭，不虚构文件内百分比。保持CreateNxProcessor的测试隔离和NoInlining，不能借测试替身绕过产品门槛。
- [x] 两代Worker项目共享源码检查、Core/Worker/Contracts测试通过；本地提交：feat: report sequential NX work progress without changing results。

## Task 3：累计快照监视器与纯显示状态

**Files:** 新增 App/Runtime/RunProgressMonitor.cs、App/Progress/RunProgressState.cs、PhaseProgressMath.cs；新增 App.Tests/RunProgressMonitorTests.cs、PhaseProgressMathTests.cs（App为src/NxDrawingPdfExporter.App）。

**Interfaces:**
- RunProgressMonitor(runDirectory,runId,total,receive,reportIssue)，StartAsync(CancellationToken)、StopAsync()，IAsyncDisposable；读取器/时间可注入，250ms轮询，不重叠读。
- RunPhase：Idle、Preparing、Initializing、Exporting、CheckingWorkerResult、Publishing、Finished、Cancelled、Failed。
- ExportPhaseCounts(Total,Processed,Generated,Failed,PureModel,NoValidSheets,OtherProcessed)；PublicationPhaseCounts(Total,Processed,Saved,Failed,Overwritten)，Overwritten是Saved子集。
- RunProgressState：RunId、Generation、Phase、以上两份Counts、当前阶段索引/子步骤、CurrentSourcePath、CancelRequested、详细反馈是否可用；CurrentSourcePath只由Controller从对应阶段的冻结条目映射，不接受旁路传路径。不可变快照交给UI。
- PhaseProgressMath.Segments(int total,int processed,bool active) 返回CompletedFraction/ActiveFraction/RemainingFraction；PercentText所用值未完不得舍入100。

- [x] 写32/10/active断言：0.3125、0.03125、0.65625；无active段为0；total0不除零；total1处理期间0+1，完成后1+0；极大total未完不显示100%。
- [ ] 用受控读取器测试累计快照跳seq仍正确；旧run/旧seq/非法计数忽略；取消监视器后不回调；同run阶段2不接受阶段1迟到更新。
  执行记录：监视器的序号、运行 ID、回退、暂停和恢复已有测试。阶段切换后的代次与阶段守卫经独立代码审阅。
- [x] 缺文件/原子替换竞争可恢复，连续读失败发简短一次可恢复提示；未变化的有效快照不判卡死，保持实际步骤及本地已用时间，不依赖人工Sleep测试。
- [x] 实现只作用当前run/generation的单一回调入口，取消/释放等待正在读的任务收拢；测试通过，本地提交：feat: monitor advisory progress and calculate phase segments。

## Task 4：Controller阶段、真实保存计数与取消

**Files:** 修改 App/ApplicationController.cs，可抽取ApplicationController.Progress.cs/Publication阶段辅助类；新增App.Tests/ApplicationProgressTests.cs，扩展ApplicationControllerTests.cs、ApplicationControllerNxTests.cs；必要时修改日志同步适配，不改变Core安全发布规则。

**Interfaces:**
- ApplicationController增加只读RunProgressState Progress、独立ProgressChanged事件；现有StateChanged用于其他状态，进度不反复驱动整表重建。
- Run开始重置计数/代次；冻结readyItems确定N，启动monitor并等待既有launcher；结束先StopAndDrain，再读/验证原终态result，计算K。
- 阶段2建立独立PublicationItems列表（按冻结顺序取可信临时成功项），当前索引从该列表解析CurrentSourcePath，不能拿阶段2索引去直接访问readyItems。
- CancelRequested在第一次取消即持有；若flag路径尚未建立，记住请求并在适当边界取消，不漏掉。是否能取消由IsBusy与此标志决定。
- 阶段2顺序等待每项既有CatalogSnapshot复核+PublishOne；耗时IO在后台执行，不读/写UI控件，必要数据取冻结快照。状态变更回同一UI调度入口；审查日志并发写入。

- [x] 写混合场景：N不含预检跳过；导出成功/失败/无页/纯模型计数分别正确；只有终态结果受信后出现阶段2，K只含临时Success；保存成功/覆盖/失败的P/S分类正确；前面含跳过/失败时阶段2当前文件仍映射正确。
- [x] 写安全反例：progress声称成功但result缺失/Completed=false/runId错，仍拒绝发布；目标/Catalog变化或临时验证失败不能增加Saved；不从progress取文件路径或结果。
- [x] 写取消时序：准备时取消、原子导出中取消、阶段1取消后保存已完成成果、仅阶段2新取消、重复点击；未执行余项不增Processed；迟到步骤不覆盖取消提示。
- [ ] 写UI响应边界：用受控发布阻塞点证明UI调度仍可收到进度/取消，逐项发布不重叠；新run重置且旧回调无效。保持原正式结果与现有测试语义。
  执行记录：已有发布阻塞点测试覆盖控制器可取消和当前文件完整收尾。真实 WinForms 调度和输入仍待验。
- [x] 实现Controller集成与独立事件；全部App相关测试通过，本地提交：fix: display export and publication progress with safe cancellation。

## Task 5：同条双色、状态文案与自适应配色

**Files:** 新增 App/Controls/SkinPhaseProgressBar.cs、Theming/ProgressPaletteResolver.cs、Progress/ProgressTextFormatter.cs；修改MainForm.Designer.cs、MainForm.cs、MainForm.Appearance.cs、Localization/Strings.resx和Strings.en.resx；新增ProgressPresentationTests.cs。

**Interfaces:**
- ProgressVisualPalette(Track,Completed,Active,Outline,Text)；ProgressPaletteResolver.Resolve(PaletteAnalysis?,ThemeDefinition,ThemeParameters,TextPalette?)，复用PerceptualColor/ColorContrast；不依赖当前GroupStyle。
- SkinPhaseProgressBar.SetProgress(RunProgressState)、SetPalette(ProgressVisualPalette)；真实段宽来自Task3；活动段内纹理仅表示忙，生命周期结束停止动画。
- ProgressTextFormatter.Format(state,UiText)返回阶段标题、计数/百分比、从state.CurrentSourcePath取得的当前文件/步骤；完整文件名可换行或悬停，不显示无法证实的“已保存”。

- [x] 写展示测试：phase1为已生成、phase2为已保存；跨阶段明确切换标题/分母，旧导出汇总保留；0/K0、仅跳过、失败和取消不出错误百分比；中英文状态完整。
- [ ] 写颜色测试：深浅/实际背景均有可区分已处理/活动/轨道，纹理与文字图例辅助；换目录组不变角色色，换皮肤不改进度数据。默认工程起点段色对轨道亮度对比≥3，段间有明显色差/分隔；文字≥4.5。
  执行记录：深浅纯色的轨道对比有数值测试。照片模式下的进度配色观感待验。
- [ ] 接入固定底部操作区，足够高度容纳百分比/阶段/文件信息，最小窗口和DPI可用；取消请求后按钮立即禁用并改文案，不被UpdateDynamicState重新启用。
  执行记录：已接入固定底部，取消按钮绑定运行状态。最小窗口和 DPI 待验。
- [x] 普通进度消息只刷新文本和进度控件，不能刷新整张表格或启动整图分析；避免用UI线程阻塞动画来伪装暂停。
- [x] App测试通过；准备用户阶段1/2、长文件、取消、深浅配色截图清单，本地提交：feat: render themed two-segment phase progress。

## Task 6：双NX证据、完整回归和候选

**Files:** 新增docs/verification/adaptive-groups-phase-progress/README.md、manual-acceptance.md；按需新增tools/verify-phase-progress-nx.ps1，复用现有受控驱动；更新README候选说明。记录脚本/说明，不提交私有图纸、PDF、日志或渲染图。

- [x] 对照Spec和两份计划逐项检查；允许独立只读代码/证据审阅。重点检查进度旁路不可触发发布、取消两阶段语义、组身份与运行结果、一份活动段、UI调度和缓存。
- [x] 在受控副本上依次运行NX10与NX2506：捕获Worker尚未退出时的有效快照、至少一个文件处理中及累计完成变化；检查真实最终result/PDF及源/模型哈希。至少验证正常多文件和取消收尾，其他失败/异常边界先通过注入测试；不以假Worker测试声称NX实测。
- [x] 本轮真实PDF沿现有结构与渲染流程核对，NX2506旧图标注差异如实保留。真实门槛不可用或源哈希变化时报告，不能伪造替代。
- [x] 执行现有publish-portable.ps1：在同一轮完成全量Release测试/构建/自包含打包，使用现有缓存和全新ReleaseRoot；遇到实际失败才独立定位并复验，已通过的同一源码不无故重复全套。
- [x] 包内两代Worker与GUI同源、素材/英文资源完整，干净解压静态检查、ZIP SHA-256和源码提交明确。未做GUI启动/DPI/实体断网不填通过。
- [x] 交付用户可逐项操作的验收清单，明确三表含运行结果的整行组色、两阶段真实数量与双色占比、取消及主题切换。完成全部可自主工作后交付待验候选，最终全项验收以实际证据为准。
- [x] 小步本地提交及最终摘要，不覆盖旧包、不推送、不打标签或创建Release。

## 自审

Contracts先定义结构，再由Core/Worker报告、App监视、Controller解释、UI呈现；进度旁路没有改变job/result的权威性。两份计划可分别验收功能，顺序集成共有的MainForm和主题代码。五项Review Focus均对应失败测试或真实证据项。实际运行记录见 [验证记录](../../verification/adaptive-groups-phase-progress/README.md)。
