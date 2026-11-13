# 2.1.0 分组色与分阶段进度验证

## 下拉框修订候选 r3

验收使用 `artifacts/release/adaptive-groups-phase-progress-candidate-20261113-r3/clean extraction`。包内源码提交为 `248bd79e9a8195b1d5f92ca349c49f808ba3d449`，ZIP SHA-256 为 `8010DFB5B2660CFCCFE6BA8CB32812D5D2E4F15AB39E1D34B506049B78CD0858`。此前候选保留。

纯色主题已有分组填充材料，但无底图。之前皮肤代码据材料存在启用下拉框自绘，绘制函数随后因无底图返回，导致选中值和展开选项空白。NX 版本、主题、渐变方向和融合风格共用该控件。修订让有底图时保留融合绘制，并在纯色、照片可见度为零和高对比模式下使用原生绘制。照片自绘的行高随字体和 DPI 更新。

Release 测试 308 项通过（App 166、Core 113、Contracts 26、Worker 3），构建零错误，保留既有 NU1702 测试项目警告。核对 27 个解压文件、13 个主题素材和 218 对中英文资源。离屏构造实际主窗口，核对菜单入口、三张表及列、两个高度手柄和固定进度区。真实桌面操作、截图及实体断网按[人工验收清单](manual-acceptance.md)复验。构建记录在 `artifacts/adaptive-groups-phase-progress-candidate-20261113-r3.log`。

r3 从干净解压目录启动两代 Worker，分别通过双组 0 版、新建 R101、覆盖 0 版、取消四个场景。每次图纸和关联模型 SHA-256 一致，两代均捕获第二项活动时首项已处理。10 份 A2 PDF 渲染图与之前人工核对的图像逐字节一致。NX 2506 旧图的角度和尺寸标注外观差异仍存在。受控驱动的记录在 `artifacts/phase-progress-final-20261113-r3`。

## 初次候选

初次候选目录为 `artifacts/release/adaptive-groups-phase-progress-candidate-20261113`。ZIP 同级的 `candidate.sha256`、`manifest.sha256`、`source-manifest.sha256` 分别对应压缩包、包内文件和构建输入。`clean extraction` 是单独解压的验收目录。上一轮候选保留在原目录，旧 ZIP 与 13 个主题素材的哈希已复核。本次 ZIP SHA-256 为 `3DE1CD4609CFCBC6AA686A95E6FA15D568E738FD9F0080E741021688DD8F8B6A`，包内源码提交为 `9d7f1ddd30b6fc67b44b6a2ba9623cd2b415a34b`。

| 验证项 | 证据 |
|---|---|
| 分组色 | App 测试验证目录键、已分配色相稳定性、8 组扩展、三种底色上的组间色差、手动字色和强度。真实 GUI 观感待验。 |
| 配置兼容 | schema 2 缺少组色强度时采用 65。损坏字段局部恢复，保留同主题的其他配置。 |
| 分阶段进度 | Contracts、Core、Worker、App 测试覆盖旁路身份和累计值、原权威快照与完成事件顺序、读取/写入失败恢复、阶段分母、单份活动段、取消和结果可信门槛。 |
| Release 与干净解压 | `artifacts/adaptive-groups-phase-progress-candidate-20261113.log` 记录 300 项通过（App 158、Core 113、Contracts 26、Worker 3）、构建零错误、既有测试项目 NU1702 警告、自包含发布、静态内容审计和解压的 27 文件清单。旧包校验复核了路径后通过。 |
| NX 10.0.0.24 和 NX 2506 | 用 `tools/verify-ui-revision-nx.ps1 -RequireProgress` 串行验证。每代各跑双组 0 版、新建 R101、覆盖 0 版、取消。每次记录绘图及关联模型 SHA-256，并核对正式 PDF。两代均从最终包解压目录启动对应 Worker。受控驱动均捕获了首项完成、第二项仍活动的中间快照，以及独立的校验并保存分母和计数。8 次会话的源及模型哈希均一致。 |
| PDF 视觉 | 10 份单页 A2 PDF（1683.78 × 1190.55 pt）通过结构核对并按 150 dpi 渲染。10 张输出图及 1 张参考图均与上轮已人工核对的对应渲染图逐字节一致。NX 2506 旧图的角度和尺寸标注外观差异是已知限制。 |
| GUI、DPI 和实体断网 | 待用户按 [人工验收清单](manual-acceptance.md) 核对。 |

正式输出仍以 `result.json`、冻结目标复核及临时 PDF 验证为依据。`progress.json` 是本次运行目录内的展示数据。双 NX 验证的私有数据、渲染图和日志只留在忽略目录 `artifacts/phase-progress-final-20261113`，未纳入候选包或版本库。
