# NX 制图批量 PDF 导出工具

这是一个面向 Siemens NX 10.0.0.24 和 Windows 11 x64 的本地便携工具项目。目标是批量读取制图 PRT，将其中含实际制图视图的 Drawing Sheet 按 NX 10 公开 `DrawingSheets` 集合返回顺序合并为同名多页 PDF。

计划中的 GUI 为 .NET 10 自包含发布，不要求用户额外安装现代 .NET；NX Worker 使用目标 Windows 11 已有的 .NET Framework 4.8 系统组件，产品不会安装或修复系统运行库。

## 当前状态

**V2 实施候选版 r2**（状态更新于 2026-09-14）。已实现有界自动发现与手动选择兜底，修复选择重入、带引号环境路径和诊断留存问题；多候选选定后仍可切换，显示根目录与版本。最近一次 Release 测试（2026-09-13）200/200 通过。用户现要求不再等待本机 NX、三档 DPI、物理断网及同事异路径验收证据，继续收尾；这些项目记为“用户放行、证据未核验”，不是实测通过。未宣称 V2 全部验证完成或已发布，也未操作远程 Issue 状态。

当前证据与待验收矩阵见 [V2 验证状态](docs/verification/v2/README.md)，
审查边界见 [实施评审交接](docs/handoffs/2026-09-08-v2-implementation-review.md)。

## 使用方式

V2 不内置开发机 NX 安装路径。自动发现读取已保存配置、注册表、已安装程序项和
当前进程环境变量；缺少有效线索时，使用“手动选择 NX 目录”，选择包含 `UGII`
的根目录。自动和手动路径均须通过 NXOpen.dll `10.0.0.24` 精确版本验证。

检测问题的脱敏诊断保存在 `%LocalAppData%\NxDrawingPdfExporter\diagnostics\run.log`，
仅包含错误代码、来源与异常类型；没有检测问题时不会创建该日志。

- 启动 `NX图纸批量导出工具.exe`（发布包）或调试构建的 `NxDrawingPdfExporter.App.exe`。
- 工具仅支持 Windows 11 x64 与本机已安装的 Siemens NX 10.0.0.24；检测不到该精确版本时会拒绝运行。
- Worker 通过 NX 自带的 `UGII\run_managed.exe` 启动，逐个处理 PRT，绝不并行。
- 导出的 PDF 先写入临时文件，经托管 PDF 校验（页数、文件头、尺寸）后才原子发布；覆盖时保留旧文件直到新文件通过校验。
- 重新发布：`powershell -NoProfile -File tools\publish-portable.ps1`，随后 `tools\inspect-release.ps1` 检查违禁内容并输出 SHA-256 清单。全程离线，不下载任何依赖。

## 已批准的核心行为

- 输入支持“扫描文件夹”和“手动多选 PRT”。
- 文件夹扫描默认不递归，可主动勾选包含子文件夹。
- 输出支持“跟随制图 PRT 目录”和“统一输出目录”。
- 已有 PDF 支持互斥的“跳过”和“覆盖”，默认跳过。
- 输出文件名保持制图 PRT 的完整基本名，只将 `.prt` 替换为 `.pdf`。
- 只有至少包含一个实际制图视图的 Drawing Sheet 才导出。
- 只有图框、标题栏、通用技术要求或预制表格的模板页跳过。
- 有效页按 NX 10 公开 `DrawingSheets` 集合返回顺序写入一个多页 PDF；NX GUI 的时间戳显示排序不参与导出。
- PDF 采用白底黑线、原始图幅、无水印，不统一缩放纸张。
- 不保存或修改 PRT，不修改 NX、注册表或全局环境变量。

## 私有样例

私有样例位于 `samples/private/`，已被 Git 忽略。制图 PRT、关联模型和参考 PDF 必须继续保存在同一目录，不得提交、重命名或对外发布。

## 设计文档

- `docs/superpowers/specs/2026-08-26-nx-drawing-pdf-exporter-design.md`
- `docs/superpowers/plans/2026-08-26-nx-drawing-pdf-exporter-implementation.md`
- `docs/prompts/2026-08-26-opencode-development-prompt.md`

## 外部开发边界

外部开发者或编码模型开始工作前必须完整阅读 `AGENTS.md` 和正式设计。没有已批准的实施计划时，不得开始产品代码、下载依赖或修改本机环境。
