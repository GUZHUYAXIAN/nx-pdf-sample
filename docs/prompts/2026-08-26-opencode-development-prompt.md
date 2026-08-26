# OpenCode 开发执行提示词

下面横线之间的内容可整体复制给 OpenCode 中的开发模型。

---

你现在负责实现一个已有正式规格和逐任务计划的 Windows/NX 自动化项目。请直接在以下现有独立 Git 仓库中工作：

`E:\Codex\projects\nx-pdf-sample`

你的任务是完整执行已批准的 V1 实施计划，而不是重新做需求讨论或扩展范围。

开始前必须按顺序完整阅读：

1. `E:\Codex\projects\nx-pdf-sample\AGENTS.md`
2. `E:\Codex\projects\nx-pdf-sample\README.md`
3. `E:\Codex\projects\nx-pdf-sample\docs\superpowers\specs\2026-08-26-nx-drawing-pdf-exporter-design.md`
4. `E:\Codex\projects\nx-pdf-sample\docs\superpowers\plans\2026-08-26-nx-drawing-pdf-exporter-implementation.md`

读完后先做只读核验：`git status`、当前分支、基线提交、本机 NX 路径及程序集版本、可用 SDK 路径、.NET Framework 4.8 状态、私有样例存在性。不要打印私有图纸内容。若工作区干净，则从当前 `main` 创建并切换到：

`feature/nx-drawing-pdf-exporter-v1`

不要创建嵌套仓库，不要创建 Git worktree，不要修改相邻项目。`E:\Codex\projects\nx-step-launcher` 只允许只读参考。

开发 SDK 固定只读调用：

`E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe`

所有 `DOTNET_CLI_HOME`、`NUGET_PACKAGES`、`NUGET_HTTP_CACHE_PATH` 必须指向当前仓库自己的 `.tools` 子目录，不能写入相邻项目或全局位置。不得安装 SDK、包或工具。实施计划批准的 NuGet 依赖只有 MSTest 4.0.2 与 PDFsharp Core 6.2.4；PDFsharp 为 MIT，仅用于 GUI 侧只读检查 PDF 页数和页面尺寸，并须随发行包附带许可证文本。如果本地缓存不足而还原需要联网下载，立即暂停，只向用户报告缺少的准确包、官方来源、版本、落盘位置、许可证、网络与发布影响，等待明确授权；不得自行下载或换包。

严格按实施计划 Task 1 到 Task 12 顺序执行，并持续更新计划中的复选框。确定性逻辑必须测试先行：先写测试，运行并确认因缺少行为而失败，再写最小实现，运行通过后重构。每个任务完成后运行对应验证并做计划指定的小提交。保留用户已有修改，不得 reset、覆盖或清理不明文件。不要 push，不要发布，不要创建 GitHub Release。

以下边界是硬性要求：

- 只支持 Windows 11 x64 与 Siemens NX 10.0.0.24，不宣称兼容其他 NX。
- GUI 为 .NET 10 WinForms 自包含进程，绝不能引用或加载 NXOpen。
- Worker 为 .NET Framework 4.8 x64，只能由已验证的 `D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe` 启动。
- Siemens NXOpen DLL 只从本机 NX 目录引用且 `Private/Copy Local=false`，绝不复制、提交或打包。
- 必须先完成真机门槛 1：真实取得 NX Session 与 UFSession。门槛失败就停止，不得继续做 GUI、不得模拟、不得擅自更换框架或启动器。
- 一个 PRT 是否属于制图文件、哪些页有效，都由真实 NX API 判断；不能依靠 `DWG_` 前缀、图层号、页名或文件名。
- Drawing Sheet 只有 `GetDraftingViews().Length >= 1` 才导出；仅有图框、标题栏、通用技术要求或表格的预制页跳过。
- 有视图但模型依赖缺失、加载失败或更新失败，必须标记该 PRT 失败，不能误判为空白模板页。
- 页序必须与 NX 导航器从上到下一致；先用真机证明集合顺序，若不一致再使用 UF Drafting，禁止字母排序或自然排序伪造。
- 每个制图 PRT 生成一个同名多页 PDF，只替换最后一个 `.prt` 扩展名；不依赖任何固定前缀。
- 支持扫描文件夹/手动多选、非递归/递归、跟随源目录/统一目录、跳过已有/安全覆盖。
- 统一目录的同名冲突必须在执行前把全部冲突项标出并阻止处理。
- 覆盖时先在目标目录生成唯一临时 PDF；经 NX Commit、文件头、非零、PDFsharp 页数与页面尺寸验证后，再原子替换。失败必须保留或恢复旧 PDF，绝不把残缺文件当成正式输出。
- PRT 串行处理，禁止并行 NX Session；单文件失败继续后续文件。
- 取消只在完成当前安全原子操作后、下一文件开始前生效；关闭 GUI 不得在替换 PDF 时粗暴杀 Worker。
- `result.json` 是权威结果，不能只看退出码。
- 产品运行时离线，不修改 NX、注册表、PATH、系统/用户环境变量、服务或全局配置。

私有样例位于：

`E:\Codex\projects\nx-pdf-sample\samples\private`

制图 PRT、关联模型、参考 PDF 必须保持同目录、原名、原内容。永远不得保存、覆盖、重命名、移动、删除、提交或公开这些文件。第一次真机验证开始前计算两个 PRT 的 SHA-256 基线，并只保存在 gitignored 的本次运行证据目录；每次真机验证结束后与本次验证前的值逐一比较。不得把样例哈希、文件清单或其他私有样例元数据写进 Git。

任一哈希变化都是严重失败，立即停止并报告。缺模型、损坏、名称冲突、取消等破坏性测试只能在 `artifacts/` 下的临时副本上做。生产代码不得调用 NX Save/SaveAs/SaveComponents 等保存 API。

执行时保持自主推进，只在以下情况暂停询问：需要未经授权的下载/安装；真机门槛失败；发现规格与本机事实实质冲突；需要删除、覆盖私有文件、扩大范围、推送或发布。普通实现细节按计划自行完成，不要反复询问已经批准的选择。

在声称完成前，必须真实执行计划中的全部测试、Release 构建、三道 NX 真机门槛、PDF 全页渲染与视觉检查、源文件前后哈希、100%/125%/150% DPI GUI 检查、干净目录解压、断网运行、发行包禁入项扫描和占位符扫描。没有证据就不能说“完成”或“通过”。

最终只交付本地分支，不 push、不发布。最终报告必须包含：

1. 分支名及有序提交列表；
2. 实际 SDK、NX、NXOpen、.NET Framework 版本；
3. 测试、构建、发布命令和通过数量；
4. Gate 1 的真实 Session/UFSession、命令和退出码；
5. Gate 2 的全部页/有效页/模板页清单、导航器顺序证明、PDF 页数与尺寸、视觉检查位置、源文件前后哈希；
6. Gate 3 两种输入、递归、两种输出、跳过、覆盖、纯模型、无有效页、缺依赖、冲突、失败继续、取消的结果矩阵；
7. 安全覆盖失败后旧 PDF 未变化的哈希证据；
8. GUI 三种 DPI 截图、干净解压和离线验证；
9. 发行文件清单与 SHA-256，并证明无 Siemens DLL、私有样例、源码、日志和临时文件；
10. 已知限制、任何经用户批准的偏差、最终干净的 `git status`。

现在开始：先完整阅读四份文件并给出不超过 12 行的执行基线摘要，然后直接执行 Task 1。除非触发上述暂停条件，不要只停留在计划复述。

---
