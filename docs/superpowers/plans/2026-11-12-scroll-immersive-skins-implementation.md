# 整体滚动与融合皮肤 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: 使用 superpowers:executing-plans 在新对话逐项实施；本交接选择一个主执行者集成，最后允许独立只读审阅。不要自动分派并行代码编写任务。按 - [ ] 记录实际完成。用户另行指定 subagent-driven 时才改用对应流程；NX 会话始终串行。

**Goal:** 修复 2.1.0 验收中发现的分区滚动和图片皮肤问题，交付独立调高、整窗背景合成与完全离线的高对比自动配色。

**Architecture:** 只在现有 WinForms App 中增加布局策略、统一底图坐标/缓存、派生绘制控件和配色计算。保留 DataGridView 的编辑/选择与业务模型，Core/Contracts/双 Worker 的导出语义不变。

**Tech Stack:** 已有 SDK 10.0.400、net10.0-windows、WinForms、System.Drawing、MSTest；无新增依赖、安装或框架迁移。

**Spec:** E:\Codex\projects\nx-pdf-sample\worktrees\v2\docs\superpowers\specs\2026-11-12-scroll-immersive-skins-design.md

**Status:** 用户已确认设计方向并要求直接交接执行；本计划未执行，下面的测试名称是待实施用例，不是通过证据。

## Global Constraints

- 工作树 E:\Codex\projects\nx-pdf-sample\worktrees\v2，当前基线 5952467；保留无关/未跟踪文件，不能误改外层 V1。
- 采用用户新选择 B，旧方案 A 对必要自绘的限制不再适用。WinForms/双 Worker/离线/源文件保护边界保持。
- 五种纯色外观保留，复用四张现有真实角色图片；不能用右侧照片栏或不透明主表格充当融合效果。
- 文件名完整、分组映射、0/R100 任意版次、冻结计划和安全发布全部回归。
- 普通文字至少 4.5:1，核心阅读文字优先 7:1；验证最终背景的最差位置；不能仅以均值/少量采样声称达标。
- 自动遮罩可以增强有效保护，但不改写用户请求的滑杆值；滚动时字色稳定。
- ui-settings.json 迁移 schema 1 → 2；NX settings.json 不变；不丢有效旧主题、语言、导入图片。
- 现有 SDK/包缓存，只做 --no-restore 构建测试发布；不联网 restore、不安装、不修改系统。
- GUI 操作、DPI/截图和物理断网由用户执行。未取得证据即标待验，不能用单测或效果图代替。
- 不动私有 PRT/PDF，不保存源；如确需 NX 验证，串行、隔离副本、逐次制图与关联模型 SHA-256。
- 保持 2.1.0 本地候选，用新的唯一包目录和源码身份区分；不覆盖旧包、不推送、不打标签、不发布。

## Review Focus

1. 150% DPI、长名称换行和滚动条宽度改变使溢出反复切换：Task 2 的实际行高/尺寸收敛与人工矩阵。
2. 正在输入无效版次、筛选和折叠时滚轮或主题重绘丢编辑：Task 2、5、6 的编辑/会话回归。
3. 黑白混合图或仅一小块亮斑被平均色遗漏：Task 3 的全区域/保守验证及材料兜底测试。
4. 快速换图、调滑杆后取消，旧异步计算覆盖已恢复主题或访问已释放位图：Task 4、6 的版本和资源生命周期测试。
5. 旧配置升级后原皮肤丢失，手动滑杆被保护算法持久化改写：Task 1、3、6 的迁移和预览往返测试。

## 启动与命令

完整阅读本计划、Spec、AGENTS.md、README.md 和 Spec 列出的旧正式设计；本次增量规则优先。旧实施计划仅用于理解已完成功能，不重做其所有任务。

先核对 git status --short --branch、git log -1 --oneline、git diff --stat。已有隔离工作树可直接使用；允许在其内从当前完整基线创建 codex/scroll-immersive-skins，若名称已存在则先核对，不重置。只暂存本任务准确路径。

~~~powershell
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
& $taskDotnet --version
& $taskDotnet test .\NxDrawingPdfExporter.slnx -c Release --no-restore
~~~

核对实际 SDK，保存当前基线结果。全部逻辑任务先写指定失败断言，执行 App 测试项目见到对应失败，再实现并跑到退出码 0：

~~~powershell
& $taskDotnet test .\tests\NxDrawingPdfExporter.App.Tests\NxDrawingPdfExporter.App.Tests.csproj -c Release --no-restore
~~~

任务提交前执行 git diff --check 并审阅差异；不为纯色值/控件存在性编造镜像测试。没有新变化或未解决问题时不重复全量验证。

## 文件与接口约定

本计划文件路径均相对活动工作树。新增纯策略类型与现有 App.Tests 配合；可使用仓库已有 InternalsVisibleTo，不扩展 Core 依赖。内部绘制类拆分可合理微调，但下列跨任务接口与行为需同步维护。

| 文件 | 职责 |
|---|---|
| App/Configuration/UiSettings.cs、JsonUiSettingsStore.cs | schema 2、两个高度、图片参数与旧配置迁移 |
| App/Layout/WorkspaceLayoutPolicy.cs | 滚轮归属、DPI/高度纯逻辑 |
| App/Layout/WorkspaceScrollCoordinator.cs | 应用内鼠标消息适配、内外滚动，不注册系统全局钩子 |
| App/Controls/HeightResizeGrip.cs、MainForm.Layout.cs | 两个独立手柄和主体布局 |
| App/Theming/ColorContrast.cs、ImagePaletteAnalyzer.cs、SurfaceContrastResolver.cs | 离线分析、亮度/对比、有效遮罩 |
| App/Theming/ThemeFrame.cs、ThemeRenderCoordinator.cs、BackdropMapping.cs | 最新完整帧、取消/所有权、公共坐标 |
| App/Controls/SkinSurface.cs、SkinDataGridView.cs、SkinScrollBar.cs | 表面/表格/滚动条显示与输入适配 |
| App/Theming/ThemeControlStyler.cs | 菜单、编辑控件、提示、按钮与产品弹窗状态 |
| App/MainForm.Appearance.cs、MainForm.Workspace.cs、MainForm.Designer.cs、Dialogs/AppearanceDialog.cs | 现有界面接入；不改会话业务语义 |

上表 App/ 是 src/NxDrawingPdfExporter.App/ 的简写；测试在 tests/NxDrawingPdfExporter.App.Tests/。

## Task 1：配置迁移和可测试的布局规则

**Files:** 修改 Configuration/UiSettings.cs、Configuration/JsonUiSettingsStore.cs；新增 Layout/WorkspaceLayoutPolicy.cs；修改 UiSettingsTests.cs，新增 WorkspaceLayoutPolicyTests.cs。

**Interfaces:**
- WorkspaceHeights(int PreflightDip = 280, int ResultsDip = 320)，Normalize() 返回 120–2000 的合法值。
- UiSettings.SchemaVersion = 2，WorkspaceHeights 属性；旧 SplitterRatio 只为兼容迁移读取，不再控制新布局。
- ThemeParameters 追加具默认值的属性，保留已有构造调用兼容：PanelOpacity=30、SurfaceMode=Dark、AutomaticTextColor=true、ManualTextColorArgb=null、GradientDirection=LeftToRight；定义对应枚举及 Normalize。
- ScrollExtent(int Offset, int MaximumOffset)；WheelOwner 为 None/Local/Workspace。
- WorkspaceLayoutPolicy.RouteWheel(bool overList, ScrollExtent local, ScrollExtent workspace, int direction)，direction=1 向下，-1 向上。
- WorkspaceLayoutPolicy.ResizeHeightDip(int startDip, int pointerDeltaPixels, int dpi, bool topHandle)，底部手柄向下增高、顶部手柄向上增高，限制到保存边界；渲染时另考虑当前最小表头/行高。

- [ ] 写失败用例：schema 1 中英文/角色主题/导入图/旧效果/字号/列宽迁移全部保留；两个新高度是 280/320；未知 schema/损坏字段恢复可解释。schema 2 往返保留新参数，未知字段不破坏旧有效值。
- [ ] 写布局断言：空表与方向边界交给 Workspace；内部仍可滚返回 Local；内外均到底返回 None；顶部能向下滚时不因 Offset=0 错误转外层。
- [ ] 写 DPI 断言：start=280、delta=150、dpi=144，底部得到380、顶部得到180；非法高度被约束，重复保存/恢复不累积缩放。
- [ ] 运行 App 测试见到新失败；实现迁移与纯策略；运行 App 测试通过。
- [ ] 审阅并本地提交：feat: add independent workspace sizing and settings migration。

## Task 2：连续页面和内外滚动

**Files:** 修改 MainForm.Designer.cs、MainForm.cs、MainForm.Workspace.cs；新增 MainForm.Layout.cs、Controls/HeightResizeGrip.cs、Layout/WorkspaceScrollCoordinator.cs；扩展 WorkspaceLayoutPolicyTests.cs。

**Interfaces:**
- HeightResizeGrip(bool topHandle)，HeightRequested 事件传递新的 DIP 高度，键盘与拖动共用 Task 1 的策略。
- WorkspaceScrollCoordinator(Control root, ScrollableControl viewport, IReadOnlyList<DataGridView> grids) : IDisposable，注册/释放仅本窗体内事件适配。
- MainForm.Layout.cs 中 UpdateWorkspaceLayout() 负责主体内容高度和两个视口高度；不调用重新规划或 RenderRows 来实现滚动。

- [ ] 用构造的实际行高数据测试溢出：行高24+60、表头28、可用112能放下；扣除横向条后不能放下。显示/隐藏条引起宽度变化后可用宽度求解应收敛。
- [ ] 扩展滚轮策略/适配测试：一次事件仅有一个消费者；小 delta 累积；已打开弹层不会传给主体。运行失败用例。
- [ ] 将菜单/底部操作固定，中间改为一个自动滚动主体；移除设置高度上限和单独滚动，设置摘要与内容同处主体。拆掉原 split 对两表高度的绑定。
- [ ] 放置预检查下方手柄、结果标题下/列头上手柄。只更新对应高度；应用 schema 2、最小尺寸、恢复布局；手动清单按实际溢出滚动。
- [ ] 接入指针命中、内部边界与主体转交。保留编辑器焦点、选中项、行身份、筛选和会话值；无效版次编辑不能被强制取消。保持文件名完整换行。
- [ ] 运行 App 测试和已有会话回归；人工清单记录两手柄位置/方向、三表少量/大量行、多目录组、小窗口/DPI，GUI 结果未取得时保持待验。
- [ ] 本地提交：fix: scroll the workspace and resize each table independently。

## Task 3：离线字色与保护材料算法

**Files:** 新增 Theming/ColorContrast.cs、ImagePaletteAnalyzer.cs、SurfaceContrastResolver.cs；新增 ColorContrastTests.cs、SurfaceContrastTests.cs。

**Interfaces:**
- ColorContrast.RelativeLuminance(Color color)、Ratio(Color foreground, Color background) 返回 double；颜色先按最终不透明合成结果计算。
- PaletteAnalysis(IReadOnlyList<Color> DominantColors, double MinimumLuminance, double MaximumLuminance)；ImagePaletteAnalyzer.Analyze(Bitmap bitmap)。
- TextPalette(Color Body, Color Secondary, Color Selected, Color Success, Color Warning, Color Error)。
- SurfaceContrastResult(Color Foreground, Color OverlayColor, int EffectiveOpacity, double WorstContrast, bool Protected)。
- SurfaceContrastResolver.Resolve(IReadOnlyList<Color> backgrounds, Color foreground, Color preferredOverlay, int requestedOpacity, double desiredRatio = 7, double minimumRatio = 4.5)。结果由真正合成后的最差值决定；foreground 保持稳定，必要时调整 OverlayColor 和有效不透明度。
- 候选色生成/语义色明暗调整可为内部方法，但以相同输入得到相同结果，不使用网络或随机结果。

- [ ] 写数值反例：黑白21:1、同色1:1；#777777 对白约4.478且不得判为4.5达标；纯色数组与混合背景数组的最差值不同。
- [ ] 写图片/区域用例：暗蓝、浅暖、纯黑、纯白、黑白条纹、仅一个白像素的暗底、透明 PNG 预合成。仅均值达标但最差不达标必须触发保护。
- [ ] 写保护断言：白字对黑白混合图需要额外遮罩，最终所有背景样本至少4.5；可达7的场景达到7。适配器的完整像素/保守边界验证不能漏掉单像素亮斑。requestedOpacity 在调用前后不变。
- [ ] 写色板用例：相同照片/效果重复分析稳定；不同色调有协调候选；失败/成功语义色不互换；手动中灰色无法达到7时仍须找到至少4.5的材料方案或明确回退。
- [ ] 运行失败测试；实现 sRGB 线性化（0.04045 分段，亮度系数0.2126/0.7152/0.0722）及对比度 (Lmax+0.05)/(Lmin+0.05)，加入最小必要遮罩求解、性能受控的验证缓存。
- [ ] 跑 App 测试到通过，本地提交：feat: adapt skin text and surface contrast offline。

## Task 4：共享背景、最新帧与资源生命周期

**Files:** 修改 Theming/BackgroundRenderer.cs、MainForm.Appearance.cs；新增 Theming/ThemeFrame.cs、ThemeRenderCoordinator.cs、BackdropMapping.cs；新增 BackdropMappingTests.cs、ThemeRenderCoordinatorTests.cs。

**Interfaces:**
- ThemeRenderRequest(long Revision, string? ImagePath, Size PixelSize, ThemeDefinition Theme, ThemeParameters Effects, bool HighContrast)。
- ThemeFrame : IDisposable，包含 Revision、Backdrop（唯一所有者 Bitmap）、Palette；绘制使用只读帧。保护材料缓存按背景/区域/状态依赖失效。
- ThemeRenderCoordinator.Request(ThemeRenderRequest request)，仅对最新请求交付完整帧；取消/释放后不发回调。渲染器可注入受控测试委托。
- BackdropMapping.SourceRegion(Rectangle rootClient, Size bitmapSize, Rectangle destinationInRoot) 返回 RectangleF。
- IBackdropSource.PaintBackdrop(Graphics graphics, Rectangle localBounds, Point originInRoot)；originInRoot 为本控件原点在同一窗体客户区的实际像素坐标。

- [ ] 写坐标测试：窗口1000×800、缓存500×400、客户区片段(100,60,200,100)，源片段应为(50,30,100,50)；相邻面板边界一致，主体滚动只改变控件命中位置，不给底图叠加内容行偏移。
- [ ] 写受控异步用例：A 比 B 晚完成只呈现 B；预览取消提交恢复请求后旧预览不能重新出现；关闭时迟到位图被释放，当前帧不在绘制中被释放，失败保留上一完整有效帧。
- [ ] 运行失败测试；实现缩放/位置、独立图片/遮罩参数、模糊/雾化/渐变方向与纯色/系统高对比路径。使用缓存和节流，滚动/单元格重绘不触发全图处理。
- [ ] 删除右侧 artwork 控件/列宽及侧图渲染；全客户区共用一张底图，确保透明 PNG 已与主题底色合成。
- [ ] 跑 App 测试；记录纹理缓存大小、连续换图后的资源释放检查方式；本地提交：feat: compose a shared and cancellable skin backdrop。

## Task 5：真实表格、编辑器与全部状态融合

**Files:** 新增 Controls/SkinSurface.cs、SkinDataGridView.cs、SkinScrollBar.cs、Theming/ThemeControlStyler.cs；修改 MainForm.Designer.cs、MainForm.Workspace.cs、MainForm.Appearance.cs；按实际共享策略新增 SkinStateTests.cs。

**Interfaces:**
- SkinDataGridView : DataGridView，BackdropSource 属性接受 Task 4 的 IBackdropSource；颜色/状态由 TextPalette 与 SurfaceContrastResult 提供。
- SkinSurface 同样消费公共背景。ThemeControlStyler.Apply(Control root, ThemeFrame frame) 只更新外观，不重建业务行/控件以清除编辑。
- 滚动条范围与真实显示内容绑定；复用 Task 2 的路由，不新增第二套冲突的 wheel 处理。

- [ ] 对可抽离的状态合成写失败用例：普通/选中/错误/编辑状态各自对最终背景校验；组标签和色层身份跨选中保留；主题切换不写入预检/目录映射/版次模型。
- [ ] 先在预检查表格实现完整链路：单元格/表头/空白/边界、组底纹、选中/多选/焦点、可编辑版次与校验提示。标准编辑框可采用更厚保护材料，仍需同主题、无突然白底，保留中文输入法、复制粘贴和键盘。
- [ ] 将共用能力扩展到手动清单、结果表格和输出目录行。派生控件保留原事件与 GridRow 数据契约，不重写导出规划。
- [ ] 完成实际滚动条、菜单、按钮、输入框、提示和产品弹窗的状态绘制；保留系统标题栏/文件选择器。系统高对比进入/退出正常。
- [ ] 跑 App 测试、已有业务测试；为真实 WinForms 验收准备“预检查表格整套状态”操作样例。静态图片/HTML/离屏绘图测试均不能标记 GUI 验收通过。
- [ ] 本地提交：feat: integrate table interactions with immersive skins。

## Task 6：外观交互与完整取消恢复

**Files:** 修改 Dialogs/AppearanceDialog.cs、MainForm.Appearance.cs、Localization/Strings.resx、Strings.en.resx、Configuration/UiSettings.cs；扩展 UiSettingsTests.cs、ThemeTests.cs、ThemeRenderCoordinatorTests.cs。

**Interfaces:** 沿用现有预览回调的主题ID/ThemeParameters/图片引用，增加参数无需把业务会话混入；对话框维护进入时快照、编辑副本和已应用快照。所有预览请求通过 Task 4 的代次控制。

- [ ] 写失败断言：更换图+调所有参数+取消，恢复旧完整外观与原保存值；应用后重启还原新参数；恢复当前预设不清其他预设；预览中的自动保护不写回 PanelOpacity。
- [ ] 写导入失败/丢图/坏配置用例；旧主题参数补新默认值；四张预设和导入图都调用同套自动对比路径。
- [ ] 接入图片强度、面板遮罩、模糊、雾化、渐变强度/方向、缩放/位置、深/浅材料、自动字色与手动微调/恢复自动；所有标签/状态有中英文。
- [ ] 当自动增强遮罩时显示简短说明，保留滑杆原值；不增加反复确认、不逐行弹窗、不在主工作区重新添加照片预览栏。
- [ ] 快速调节、切换语言、取消/关闭与后台处理失败都不丢正在编辑的版次。跑 App 测试和全部相关会话回归。
- [ ] 本地提交：feat: expose adaptive skin controls and restore previews safely。

## Task 7：回归、独立审阅、本地候选和验收交接

**Files:** 新增 docs/verification/scroll-immersive-skins/README.md、manual-acceptance.md；按实际更新 README.md。本任务不重写已有角色图、Worker 协议或导出业务。

- [ ] 自查每条 Spec 到任务/证据的覆盖，运行 git diff --check；核对 Core/Contracts/Worker 未被意外改动。若发现实际依赖才说明并调整范围，不凭假设扩大。
- [ ] 全量 Release 测试与构建，记录真实命令/退出码/计数。优先让下面的既有发布脚本在同一轮完成测试、构建和打包；已经完成同一源码快照检查时不另行重复。仅需单独定位问题时使用：

~~~powershell
& $taskDotnet test .\NxDrawingPdfExporter.slnx -c Release --no-restore
& $taskDotnet build .\NxDrawingPdfExporter.slnx -c Release --no-restore
~~~

- [ ] 允许独立只读审阅代理检查实际代码与证据，重点看全区域对比、图层坐标、编辑/焦点、滚轮转交、预览取消、配置迁移。修复实际问题并只重跑受影响检查；不启动并行 NX。
- [ ] 在跟踪文件已提交且身份明确后，使用现有 publish-portable.ps1 和全新 ReleaseRoot 创建 2.1.0 本地验收包，运行已有包检查、干净解压静态完整性检查并给出 ZIP SHA-256：

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish-portable.ps1 -PackageCache .nuget -ReleaseRoot artifacts/release/新的唯一目录 -DotnetPath $taskDotnet
~~~

- [ ] 提供人工清单：Spec §7 全部场景、具体按钮/鼠标位置、预期结果、截图建议；明确区分“自动通过”“用户待验”“历史 NX/PDF 证据”。不得复用旧截图证明新 B 皮肤或把未收集的 DPI/物理断网结果填通过。
- [ ] 按项目要求记录 NX/PDF/source-hash 证据的适用版本。若本轮运行真实 NX，遵守串行/隔离副本/逐次哈希；若未运行，明确写“本轮未重跑”，保留 NX2506 既有标注视觉差异。这不妨碍交付待验候选，不等同全项验收完成。
- [ ] 最终交付改动摘要、测试/构建结果、新 ZIP/哈希/源码提交、视觉人工清单和已知限制；不推送、不创建 Release。

## 自审结果

- 布局与皮肤共用客户区坐标和重绘机制，因此按一个连续实施计划交付；不拆成两个互相覆盖 MainForm 的并行开发任务。
- 配置类型在 Task 1 定义，配色结果在 Task 3 定义，共享帧在 Task 4 定义，后续只消费这些接口。
- 五项 Review Focus 已映射到对应的确定性测试或实际 GUI 清单；实际 GUI 待验不伪装成测试通过。
- 新增类名是实施约定，内部分解可以改善；若调整接口，须同时更新使用方和验证，不削减用户要求。
