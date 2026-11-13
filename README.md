# NX 制图批量 PDF 导出工具

这是一个 Windows 11 x64 本地便携工具，批量读取制图 PRT，将包含实际制图视图的 Drawing Sheet 按 NX 公开 `DrawingSheets` 集合顺序导出为同名多页 PDF。

## 2.1.0 分组色与分阶段进度候选

手动清单、预检查、运行结果和输出目录按源目录统一着色。外观设置可独立调整组色强度。执行区先显示 Worker 的导出进度，再显示临时 PDF 的校验并保存进度。构建、双 NX 验证及待验操作见 [本轮验证记录](docs/verification/adaptive-groups-phase-progress/README.md)。

## 前一轮 2.1.0 整体滚动与融合皮肤候选

本轮候选将设置、预检查与结果排列在同一个主体滚动区，预检查和结果可独立调高。角色皮肤和导入图共用窗口底图，字色和可读性遮罩在本地计算。验收范围与操作见 [整体滚动与融合皮肤验证记录](docs/verification/scroll-immersive-skins/README.md)。

## 上轮 2.1.0 本地候选

本地迭代新增按源目录分组输出、可编辑数字版次、双语界面和四套离线角色皮肤。此候选采用协议 2，需保留完整 ZIP 中的两代 Worker、英文资源和主题素材。验证范围及待验项目见 [2.1.0 验证记录](docs/verification/ui-revision-skins/README.md)。

## 已发布版本

**v2.0.1：NX 10 / NX 2506 双版本更新（2026-09-19）**。一个 .NET 10 自包含 GUI，两个独立 net48 x64 Worker；支持发现、选择、保存和切换。[下载便携版](https://github.com/GUZHUYAXIAN/nx-pdf-sample/releases/tag/v2.0.1)。已验证组件组合：

| 版本 | NXOpen | NXOpen.UF | NXOpen.Utilities | 启动入口 |
|---|---|---|---|---|
| NX 10 | 10.0.0.24 | 10.0.0.23 | 4.0.0.0 | UGII/run_managed.exe |
| NX 2506 | 2506.0.0.0 | 2506.1700.0.0 | 2506.0.0.0 | NXBIN/run_dotnet_nxopen.exe |

未知补丁或混搭安装拒绝运行。本机两代真实会话、PDF 结构检查和副本批量回归通过，自动化 223/223；发布内容和验证范围见 [v2.0.1 发布记录](docs/releases/v2.0.1.md)。

维护者已手动试用当前版本并确认问题修复，授权发布。本轮自动化曾观察到旧图纸跨版本标注显示差异，历史诊断证据保留；手动反馈没有附带逐项截图和日志，因此不扩大为所有图纸、补丁或机器均已验证。跨 NX 版本处理旧图纸时应核对尺寸标注。

100/125/150% DPI 完整矩阵、物理断网和其他电脑验收仍未逐项记录，详见 [完整人工清单](docs/verification/dual-nx/manual-acceptance.md)。发布不表示这些项目已经通过。

历史 `v2.0.0-rc.1` 为仅 NX 10 的旧 V2 预发布资产；历史 [V2 验证状态](docs/verification/v2/README.md) 和放行决定保留，不能作为本次新增功能的实测证据。

## 使用方式

1. 解压完整候选 ZIP，保留 `worker/nx10` 和 `worker/nx2506` 两个目录，启动 `NX图纸批量导出工具.exe`。不要混用旧包 Worker。
2. 程序读取保存项、限定注册表/已安装程序项及进程环境。仅一个有效安装自动选中；两个有效安装无有效保存项时必须选择。界面显示版本与路径，有效保存项优先，不自动切到新版。
3. 手选 NX 根目录：NX 10 包含 UGII，NX 2506 包含 NXBIN；同样执行三组件精确验证。无效选择不覆盖原有效选择。运行中不能换版本。
4. 选择扫描目录或手动多选 PRT，选择输出方式并预检查。已有输出默认跳过；覆盖选项只在临时 PDF 验证成功后替换旧文件。
5. 对旧图纸优先用其原始 NX 版本验证；工具同时支持两代并不意味着 NX 10 可以读取 NX 2506 保存的 PRT。

设置保存在 `%LocalAppData%\NxDrawingPdfExporter\settings.json`（兼容 schema 1）。检测问题的脱敏日志在同目录 `diagnostics/run.log`；单次运行证据在 `runs/<runId>`。日志不会上传，运行目录可能含私有任务路径和图纸名称，不得公开。

GUI 不加载 NXOpen。Worker 通过所选安装启动，子进程环境独立；产品运行不下载、不安装、不修改 NX 或系统配置。成功以当前任务的完整 result.json 及 PDF 检查为准，不只看启动器退出码。

## 离线构建与本地打包

使用 `global.json` 所要求的现有 SDK 10.0.400、本地已安装 NX 和现有 NuGet 缓存。所有产品测试、构建、发布都用 `--no-restore`。迁移目录或新项目需要 assets 时，仅可显式指定已存在缓存为唯一源离线还原；禁止默认网络 restore。

```powershell
# 在已经离线还原 assets 的 V2 目录执行；ReleaseRoot 必须尚不存在。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/publish-portable.ps1 -PackageCache .nuget -ReleaseRoot artifacts/release/新的候选目录 -DotnetPath '现有 dotnet.exe 的完整路径'
```

脚本执行全量测试、完整 Release 构建和自包含发布，严格从两套独立 Worker 目录取文件；生成 ZIP、相对路径 SHA-256 清单及已跟踪源码身份。正式版本增加 `-ReleaseVersion 2.0.1`，要求已跟踪工作区干净，且版本与 Directory.Build.props 一致。包检查拒绝 Siemens DLL、PRT/PDF、PDB、日志、源码和开发机路径。干净解压后的 GUI/DPI/物理断网仍需人工检查。

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

本项目自有源码和文档采用 [MIT 许可证](LICENSE)。第三方组件仍遵循各自许可证，
详见 [第三方声明](THIRD-PARTY-NOTICES.txt)。Siemens NX、NXOpen 程序集和私有样例
不包含在本项目的 MIT 授权或公开分发范围内；使用工具仍需自行具备合法可用的 NX 环境。

源码开源不代表已完成全部真机验收；验证范围与未核项目见上方“当前状态”。

## 设计文档

- `docs/superpowers/specs/2026-08-26-nx-drawing-pdf-exporter-design.md`
- `docs/superpowers/plans/2026-08-26-nx-drawing-pdf-exporter-implementation.md`
- `docs/prompts/2026-08-26-opencode-development-prompt.md`

## 外部开发边界

外部开发者或编码模型开始工作前必须完整阅读 `AGENTS.md` 和正式设计。没有已批准的实施计划时，不得开始产品代码、下载依赖或修改本机环境。
