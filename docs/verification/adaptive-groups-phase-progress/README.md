# 2.1.0 分组色与分阶段进度验证

本轮候选目录为 `artifacts/release/adaptive-groups-phase-progress-candidate-20261113`。ZIP 同级的 `candidate.sha256`、`manifest.sha256`、`source-manifest.sha256` 分别对应压缩包、包内文件和构建输入。`clean extraction` 是单独解压的验收目录。上一轮候选保留在原目录。

| 验证项 | 证据 |
|---|---|
| 分组色 | App 测试验证目录键、已分配色相稳定性、8 组扩展、三种底色上的组间色差、手动字色和强度。真实 GUI 观感待验。 |
| 配置兼容 | schema 2 缺少组色强度时采用 65。损坏字段局部恢复，保留同主题的其他配置。 |
| 分阶段进度 | Contracts、Core、Worker、App 测试覆盖旁路身份和累计值、原权威快照与完成事件顺序、读取/写入失败恢复、阶段分母、单份活动段、取消和结果可信门槛。 |
| Release 与干净解压 | `artifacts/adaptive-groups-phase-progress-candidate-20261113.log` 记录全量测试、构建、自包含发布、静态内容审计、解压清单与旧包校验。 |
| NX 10.0.0.24 和 NX 2506 | 用 `tools/verify-ui-revision-nx.ps1 -RequireProgress` 串行验证。每代各跑双组 0 版、新建 R101、覆盖 0 版、取消。每次记录绘图及关联模型 SHA-256，并核对正式 PDF。验证从最终包解压出的 Worker，进度观察由受控驱动完成。 |
| PDF 视觉 | 验证单页 A2 结构，并按 150 dpi 渲染，和以前人工核对的对应版本图像比较。NX 2506 旧图的角度和尺寸标注外观差异是已知限制。 |
| GUI、DPI 和实体断网 | 待用户按 [人工验收清单](manual-acceptance.md) 核对。 |

正式输出仍以 `result.json`、冻结目标复核及临时 PDF 验证为依据。`progress.json` 是本次运行目录内的展示数据。双 NX 验证的私有数据、渲染图和日志只留在忽略目录 `artifacts/phase-progress-final-20261113`，未纳入候选包或版本库。
