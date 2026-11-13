# 自适应分组填充色 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: 使用 superpowers:executing-plans 逐项完成，一个主执行者集成，不自动并行写代码。完成本计划后执行同日 phase-progress 计划。按 - [ ] 记录真实完成。

**Goal:** 四处列表/目录统一显示清楚、稳定、随图片和深浅模式适配的整行组色。

**Architecture:** 在现有 App 皮肤管线增加以目录身份为键的组色板和感知色差筛选，沿用现有文字对比保护与背景缓存。纯色风格和业务分组规则不变。

**Tech Stack:** C#、.NET10 WinForms、System.Drawing、现有 MSTest；本地颜色数学，无新依赖。

**Spec:** E:\Codex\projects\nx-pdf-sample\worktrees\v2\docs\superpowers\specs\2026-11-13-adaptive-groups-phase-progress-design.md

## Global Constraints

- 活动工作树 worktrees/v2，产品基线324b0a8；外层旧V1及相邻项目不改。
- 使用现有 SDK10.0.400 和缓存，--no-restore；不安装、不联网 restore。
- 手动清单/预检查/运行结果/输出行全部整行填充，A/B标记保留；不改变目录归属、源路径、目标映射或版次。
- 文字最低4.5:1、核心文字优先7:1；同时检查组色可辨识，不能用不断削弱组色作为常规保护。
- 分组强度0–100、默认65，schema2兼容添加；UI配置不新增业务路径。
- 滚动/筛选/排序/新增组不打乱已分配色相；深浅切换保留色相身份，换图可重新生成。
- 实际GUI/DPI由用户验收；不把算法或截图原型当视觉通过。只本地小步提交，不推送/发布。

## Review Focus

1. 当前输入变更但运行结果仍属于旧目录，A标签相同不能混用组身份：Task1、2。
2. 黑白高纹理背景及手动中灰字色，保护算法把组色洗没：Task1联合目标。
3. 7/8组机械重复第1/2组，新组加入重排全部颜色：Task1稳定分配。
4. 选中/悬停与状态文字的小保护底层遮掉整行或组标记：Task2真实材料状态。
5. schema2旧配置缺字段导致清空现有照片/字号，预览取消未恢复新强度：Task3。

## 启动和验证命令

先读 AGENTS/README、Spec及其引用旧设计、审阅记录，核对 git status --short --branch 与 git log -1。保留无关文件；允许当前隔离工作树内创建 codex/adaptive-groups-phase-progress，不重置或另建项目外工作树。

~~~powershell
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PWD '.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $taskDotnet --version
& $taskDotnet test .\tests\NxDrawingPdfExporter.App.Tests\NxDrawingPdfExporter.App.Tests.csproj -c Release --no-restore
~~~

后续各任务先新失败用例、同命令确认对应失败、实现、运行通过；提交前 git diff --check。新增类路径以 src/NxDrawingPdfExporter.App 为根，测试以 tests/NxDrawingPdfExporter.App.Tests 为根。不重做上轮已完成布局。

## Task 1：组身份、候选色与联合保护

**Files:** 新增 Theming/PerceptualColor.cs、GroupPalette.cs；修改 Theming/SkinMaterials.cs、ThemeFrame.cs；新增 GroupPaletteTests.cs，扩展 SkinContrastTests.cs。

**Interfaces:**
- GroupVisualKey(string DirectoryKey, string Label)，DirectoryKey 使用现有 SourceDirectoryGrouper.NormalizeDirectory；Label 仅用于呈现。
- OklabColor(double L, double A, double B)；PerceptualColor.FromSrgb(Color)、Distance(Color,Color)，采用作者已公开的sRGB转换，数值测试固定。
- GroupStyle(Color Tint, int Opacity, Color Marker, Color MarkerText, double Hue)，表示共同样式；最终材料仍由 SkinMaterials 校验。
- GroupPaletteResolver.Resolve(PaletteAnalysis analysis, ThemeDefinition theme, ThemeParameters effects, IReadOnlyList<GroupVisualKey> groups, IReadOnlyDictionary<string,double> assignedHues) 返回不可变目录键→GroupStyle。
- 当前输入与保留运行结果的目录键并集参与分配。assignedHues 为会话状态，更新后保留现有项；不放入 ui-settings.json。
- SkinMaterials 按组键/GroupStyle取 Row 材料，取消RGB反查组身份；原纯色可保留已有风格入口。

- [ ] 写稳定性失败测试：同key四处同style；不同key相同label不合并；筛选/排序/滚动不改色；加入第7/8组不重分配前6组且不简单重复；深浅切换保留Hue。
  执行记录：目录键及 8 组稳定性有单测。四处真实 GUI 和排序、滚动观感待验。
- [x] 写数值/合成失败测试：黑白Oklab距离约1、同色0；默认65在暗蓝/浅暖/灰底的2–6组夹具，合成后组对无组距离≥0.04、组间≥0.05。这些为工程起点，不声称视觉标准。
- [x] 写冲突反例：混合明暗/单亮点、手动中灰字色；文字≥4.5、可达场景≥7，不能最终所有组都回落为同一个近零透明色；正常照片不全区实色。
- [x] 跑失败测试，实现候选色/稳定分配/必要材料联调，处理超色域颜色，不通过NaN/通道裁切产生错误色差；复用现有分析而非逐行重新读图。
- [x] App测试通过，本地提交：feat: resolve adaptive directory group palettes。

## Task 2：四处接入与清晰组标记

**Files:** 修改 MainForm.Workspace.cs、MainForm.Appearance.cs、Controls/SkinDataGridView.cs、Controls/SkinSurface.cs、Theming/ThemeControlStyler.cs、ThemeFrame.cs；扩展 SkinPaintingTests.cs。

**Interfaces:** SkinDataGridView 的组提供器改为返回 GroupVisualKey（而非Color）；SkinSurface 持有相同组键。GridRow.Source→规范父目录键，GridRow.Group→标签；运行结果继续用runRows的原序号/标签，不能拿新CurrentPlan冒充其身份。

- [ ] 写接入回归：手动清单、预检、结果与输出行使用相同目录键；旧结果/新输入同标签不同目录各自保持身份；选中态与正常态仍有组标记，主文本和标记文本可读。
  执行记录：共享材料和目录键由独立代码审阅确认。旧结果、四处同色和选中显示的端到端操作待验。
- [x] 在三张表格整行绘制共同材料；目录列增加明确同色小底块/标记。运行结果的状态胶囊限制在状态文本范围，保留整行色。
- [x] 输出目录行使用共同style，编辑框可沿用自己的文字保护；不能导致目录身份色与PRT行反向变化。
- [ ] 保持选中、多选、编辑、换行、焦点、皮肤取消、系统高对比和纯色回退；仅改外观时不重建业务行或使预检失效。
  执行记录：外观逻辑已接入并审阅。鼠标、键盘和系统高对比的真实 GUI 待验。
- [x] 运行App相关测试并整理用户可验的2/6/8组、四处对应、选中态与深浅对照场景；本地提交：fix: show group fills consistently across all result surfaces。

## Task 3：独立强度、旧配置与预览

**Files:** 修改 Configuration/UiSettings.cs、JsonUiSettingsStore.cs、Dialogs/AppearanceDialog.cs、Theming/SkinPreviewSettings.cs、Localization/Strings.resx、Strings.en.resx；扩展 UiSettingsTests.cs、SkinPreviewSettingsTests.cs。

**Interfaces:** ThemeParameters 末尾新增 GroupColorStrength=65，Normalize限制0–100；保持schema2及已有构造兼容，手工ReadEffects同样读默认值。0仅隐藏整行色，组标记保留。

- [ ] 写缺字段/往返/损坏字段局部恢复测试，旧图片、全部效果、语言、字号、布局保持；预览改变强度或照片后取消恢复原值、原帧和原色相映射，取消后再缩放/新增组仍使用已恢复分配，恢复预设不清其他皮肤。
  执行记录：配置读写和缺字段/损坏字段有测试。预览回滚后缩放和新增组的真实 GUI 待验。
- [x] 增加中英文强度滑杆，默认偏清晰；不是PanelOpacity别名，不把有效保护强度保存回请求值。
- [x] 校验0/65/100对正常图的差异单调可解释，0保留组标记，100也不能牺牲文字最低对比。
- [x] App测试通过，记录视觉待验项并本地提交：feat: add independent group fill strength。

## 交接与自审

此计划可独立完成分组色修复，下一份阶段进度计划复用颜色数学和现有主题分析。两个计划顺序执行，避免同时改MainForm。五类Review Focus均有对应测试/人工场景。

自动化、构建和双 NX 证据见 [验证记录](../../verification/adaptive-groups-phase-progress/README.md)。视觉操作按人工清单验收。
