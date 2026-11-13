# NX 制图批量 PDF 导出工具

这是一个 Windows 11 x64 本地便携工具，批量读取制图 PRT，将包含实际制图视图的 Drawing Sheet 按 NX 公开 `DrawingSheets` 集合顺序导出为同名多页 PDF。

## v2.1.0 源码候选

- 按源目录分组或统一输出。预检查显示实际目标文件名，可按建议升版，也可为选中项指定数字版次。
- 手动清单、预检查、运行结果和输出目录按源目录统一着色。组色强度可单独调节，预检查与结果表可分别调整高度。
- 执行区依次显示“导出”与“校验并保存”。每个阶段分别计算已处理数量。实色代表已处理文件，条纹代表当前正在处理的一份文件。
- 支持简体中文和 English、五种纯色主题、四张离线角色预置图和本地图片导入。纯色和隐藏底图时下拉框显示正常，照片主题下选项行高随字体和 DPI 调整。

维护者试用 r3 后确认可提交到仓库。Release 配置的 308 项自动化测试和两代 NX 各 4 个受控场景均通过。正式下载仍见下方“已发布版本”。[版本说明](docs/releases/v2.1.0.md) · [验证记录](docs/verification/adaptive-groups-phase-progress/README.md) · [人工验收清单](docs/verification/adaptive-groups-phase-progress/manual-acceptance.md)

## 已发布版本

**v2.0.1：NX 10 / NX 2506 双版本更新（2026-09-19）**。一个 .NET 10 自包含 GUI，两个独立 net48 x64 Worker；支持发现、选择、保存和切换。[下载便携版](https://github.com/GUZHUYAXIAN/nx-pdf-sample/releases/tag/v2.0.1)。已验证组件组合：

| 版本 | NXOpen | NXOpen.UF | NXOpen.Utilities | 启动入口 |
|---|---|---|---|---|
| NX 10 | 10.0.0.24 | 10.0.0.23 | 4.0.0.0 | UGII/run_managed.exe |
| NX 2506 | 2506.0.0.0 | 2506.1700.0.0 | 2506.0.0.0 | NXBIN/run_dotnet_nxopen.exe |

未知补丁或混搭安装拒绝运行。本机两代真实会话、PDF 结构检查和副本批量回归通过，自动化 223/223；发布内容和验证范围见 [v2.0.1 发布记录](docs/releases/v2.0.1.md)。

v2.0.1 发布时，维护者已手动试用并确认修复。自动化曾观察到旧图纸跨版本标注差异，跨 NX 版本处理旧图纸时应核对尺寸标注。

v2.0.1 的 100%／125%／150% DPI、物理断网和其他电脑验收仍缺逐项记录。[v2.0.1 人工清单](docs/verification/dual-nx/manual-acceptance.md)

历史 `v2.0.0-rc.1` 为仅 NX 10 的旧 V2 预发布资产；历史 [V2 验证状态](docs/verification/v2/README.md) 和放行决定保留，不能作为本次新增功能的实测证据。

## 使用方式

1. 将完整便携 ZIP 解压到新目录，启动 `NX图纸批量导出工具.exe`。使用同一 ZIP 内的两套 Worker。v2.1.0 还需要同包的 `en` 和 `assets` 目录。
2. 程序读取保存项、限定注册表/已安装程序项及进程环境。仅一个有效安装自动选中；两个有效安装无有效保存项时必须选择。界面显示版本与路径，有效保存项优先，不自动切到新版。
3. 手选 NX 根目录：NX 10 包含 UGII，NX 2506 包含 NXBIN；同样执行三组件精确验证。无效选择不覆盖原有效选择。运行中不能换版本。
4. 扫描文件夹或手动选择 PRT，设置输出目录和已有 PDF 策略，再运行预检查。默认跳过已有同名 PDF。需要改版次时选择“升版”，并确认预检查列出的目标文件名。
5. 点击“开始导出”。先看已处理和已生成数量，再看校验并保存阶段的已保存数量。需要取消时，当前文件处理到安全边界后收尾。
6. 旧图纸优先用原始 NX 版本验证。NX 10 无法保证读取 NX 2506 保存的 PRT。

本地设置位于 `%LocalAppData%\NxDrawingPdfExporter`。`settings.json` 保存 NX 选择，`ui-settings.json` 保存界面设置，`theme-images` 保存导入图片。单次运行记录在 `runs/<runId>`，包含仅供界面展示的 `progress.json` 和用于正式保存前校验的 `result.json`。运行记录可能包含私有路径和图纸名称。

GUI 不加载 NXOpen。Worker 通过所选安装启动，子进程环境独立；产品运行不下载、不安装、不修改 NX 或系统配置。成功以当前任务的完整 result.json 及 PDF 检查为准，不只看启动器退出码。

## 离线构建与本地打包

使用 `global.json` 所要求的现有 SDK 10.0.400、本地已安装 NX 和现有 NuGet 缓存。所有产品测试、构建、发布都用 `--no-restore`。迁移目录或新项目需要 assets 时，仅可显式指定已存在缓存为唯一源离线还原；禁止默认网络 restore。

```powershell
# 在已经离线还原 assets 的 V2 目录执行；ReleaseRoot 必须尚不存在。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish-portable.ps1 -PackageCache .nuget -ReleaseRoot artifacts/release/新的候选目录 -DotnetPath '现有 dotnet.exe 的完整路径'
```

脚本执行全量测试、完整 Release 构建和自包含发布，严格从两套独立 Worker 目录取文件；生成 ZIP、相对路径 SHA-256 清单及已跟踪源码身份。正式打包可使用 `-ReleaseVersion <版本号>`，要求已跟踪工作区干净，且版本与 `Directory.Build.props` 一致。包检查拒绝 Siemens DLL、PRT/PDF、PDB、日志、源码和开发机路径。干净解压后的 GUI/DPI/物理断网仍需人工检查。

## 已批准的核心行为

- 输入支持“扫描文件夹”和“手动多选 PRT”。
- 文件夹扫描默认不递归，可主动勾选包含子文件夹。
- 2.1.0 候选默认按 PRT 实际父目录分组。各组可独立指定目标，也可主动选择统一输出目录。
- 已有 PDF 支持互斥的原名跳过、原名安全覆盖和升版，默认跳过。升版从源目录与实际目标目录取最高数字版次，可手动指定。
- 0 版保留完整基本名，正数版次追加 `-R数字`。正式目标始终采用预检查显示并冻结的版次。
- 只有至少包含一个实际制图视图的 Drawing Sheet 才导出。
- 只有图框、标题栏、通用技术要求或预制表格的模板页跳过。
- 有效页按所选 NX 的公开 `DrawingSheets` 集合返回顺序写入一个多页 PDF；NX GUI 的时间戳显示排序不参与导出。
- PDF 采用白底黑线、原始图幅、无水印，不统一缩放纸张。
- 不保存或修改 PRT，不修改 NX、注册表或全局环境变量。

## 私有样例

私有样例保留在外层 V1 checkout 的 `samples/private/`，活动 V2 目录内没有该子目录；仅可只读验证，不得搬入发布包。已被 Git 忽略。制图 PRT、关联模型和参考 PDF 必须继续保存在同一目录，不得提交、重命名或对外发布。

## 许可证

本项目自有源码和文档采用 MIT 许可证。第三方软件组件适用各自许可。角色预置图为 AI 生成的同人插画，角色及作品权利归各自权利人。运行工具需要用户自备 Siemens NX 环境。[MIT 许可证](LICENSE) · [第三方声明](THIRD-PARTY-NOTICES.txt) · [角色素材来源](src/NxDrawingPdfExporter.App/Assets/Themes/SOURCES.txt)

## 设计文档

- `docs/superpowers/specs/2026-08-26-nx-drawing-pdf-exporter-design.md`
- `docs/superpowers/plans/2026-08-26-nx-drawing-pdf-exporter-implementation.md`
- `docs/prompts/2026-08-26-opencode-development-prompt.md`

## 外部开发边界

外部开发者或编码模型开始工作前必须完整阅读 `AGENTS.md` 和正式设计。没有已批准的实施计划时，不得开始产品代码、下载依赖或修改本机环境。
