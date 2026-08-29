# NX 制图批量 PDF 导出工具

这是一个面向 Siemens NX 10.0.0.24 和 Windows 11 x64 的本地便携工具项目。目标是批量读取制图 PRT，将其中含实际制图视图的 Drawing Sheet 按 NX 10 公开 `DrawingSheets` 集合返回顺序合并为同名多页 PDF。

计划中的 GUI 为 .NET 10 自包含发布，不要求用户额外安装现代 .NET；NX Worker 使用目标 Windows 11 已有的 .NET Framework 4.8 系统组件，产品不会安装或修复系统运行库。

## 当前状态

截至 2026-08-26，书面规格已获用户批准，产品代码尚未开始实现。实施计划和外部开发提示词已经形成；开发者只能按批准的计划执行，并须先通过 NX Worker 真机启动门槛。

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
