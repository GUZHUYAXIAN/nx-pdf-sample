# V2 验证状态（候选版 r2，更新于 2026-09-14）

2026-09-14 用户要求将本机导出、三档 DPI、物理断网和同事异路径四项按已做无问题
处理并继续下一步。执行上不再等待其证据；记录为 **USER-WAIVED / UNVERIFIED
（用户放行、证据未核验）**，不是实测 PASS，也不据此断言用户实际未做过测试。
此前 NOT RUN 是截至 2026-09-13 的执行记录。当前仍无可审查的日志、源文件前后
哈希、PDF 或 DPI 截图；所附图片仅是此前操作清单，不是四项测试结果。
V2 保持候选版；用户已授权源码提交与开源，但不宣称原设计的全部实机完成标准
已满足。源码提交不等于发布 V2 二进制 Release，公开前仍需完成许可证选择。
当前唯一验收候选为以下指定 r2 ZIP，2026-09-10 和 2026-09-13 首包保留但已被替代。

## 候选与工作区

- 隔离目录：本项目的独立 worktree（开发机用户目录不公开）。
- 基线/HEAD：`414dc5fd6f43cf2f8fa6b97812b607405221967d`，detached；审查的是
  tracked diff 加未跟踪 V2 文件，不能仅比较两个相同 SHA。
- ZIP：`artifacts/release/v2-candidate-20260913-r2/NXDrawingPdfExporter-v2-candidate.zip`。
- ZIP SHA-256：`6C0F0C9D67D8F8D20056539F05EF2F79F827761893C57EEEFABF3D06B46516EA`。
- 同目录 `candidate.sha256` 对应 ZIP，`manifest.sha256` 对应 7 个载荷文件。
- 7 文件包通过禁入项审计；新目录解压后 7 项哈希匹配。未启动 EXE。
- 当前 r2 包来自提交前的已审查工作区；本轮源码提交不改变该历史构建来源。
  尚未从最终提交重新构建正式发布资产，不伪造候选包的提交身份。

## 自动化证据（2026-09-13）

命令在上述隔离目录执行，SDK 为既有
`E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe`。
过程环境指向源项目既有 `.tools/nuget-packages` 与 `.tools/dotnet-cli-home`；
禁用 SDK 首次证书生成和遥测。本轮没有 restore、下载或安装。

1. 接续基线：`build NxDrawingPdfExporter.slnx -c Release --no-restore
   --no-incremental --disable-build-servers`，0 错误、1 个已知 NU1702；
   随后 `test NxDrawingPdfExporter.slnx -c Release --no-restore --no-build`，182/182。
   r2 接续前再次全量 `test ... -c Release --no-restore`：192/192，没有基线失败。
2. 修复后发布脚本重新执行完整 `test ... -c Release --no-restore`：
   **200/200**（App 85、Core 94、Contracts 19、Worker 2），0 失败/跳过。
3. 发布脚本的完整 `build ... -c Release --no-restore`：0 错误、1 个已知警告。
   NU1702 实际来自 net10 Worker.Tests 引用 net48 Worker，与本轮基线相同；
   不是计划中误写的 PDFsharp 条件，也不是新增警告。
4. `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test-publish-portable.ps1`
   和同参数 `tools/test-inspect-release.ps1` 均 PASS。后者的故意坏包/篡改
   子进程报错是预期的反例验证，不是产品失败。
5. 网络 API 扫描仅匹配 `app.manifest` 的 DPI XML 命名空间，不是网络调用；
   `src` 中旧固定 NX 根目录扫描无命中。
6. 发布命令：`powershell -NoProfile -ExecutionPolicy Bypass -File
   tools/publish-portable.ps1 -PackageCache E:\Codex\projects\nx-pdf-sample\.tools\nuget-packages
   -ReleaseRoot <isolated-worktree>\artifacts\release\v2-candidate-20260913-r2`。
   结果 PUBLISH OK；没有覆盖既存候选目录。
7. `inspect-release.ps1 -PackageRoot <本轮/package> -ManifestPath <本轮/manifest.sha256>`
   结果 INSPECT OK。ZIP 解压至全新 `<本轮/clean-extraction>`，同工具指定该路径
   和原 manifest 加 `-VerifyManifestOnly`：MANIFEST VERIFY OK，7 项。

2026-09-14 文档收尾：未修改产品代码、未重新构建或运行测试、未重新打包。
复算 r2 ZIP SHA-256 与上述记录相同；对既有 clean-extraction 使用上述显式
manifest 路径复验，7 项一致。没有将旧测试记录改写成今日新运行的测试。

## 设计第 15 节逐项核对

| 项 | 状态与依据 |
|---|---|
| 1 测试 | 当前回归 200/200；历史 Task 1–2 的原始 RED 仍缺失，不能倒填 |
| 2 构建 | PASS；0 错误，只有已知 NU1702 |
| 3 本机真实 NX | USER-WAIVED / UNVERIFIED，用户放行但没有回传运行证据 |
| 4 两个源文件每轮哈希 | UNVERIFIED，没有读取或伪造样件哈希，不宣称前后一致 |
| 5 包审计与干净解压 | PASS；7 项匹配；不等于干净解压后 GUI 启动通过 |
| 6 DPI 100/125/150% | USER-WAIVED / UNVERIFIED，三档均无已复核截图 |
| 7 物理断网真实导出 | USER-WAIVED / UNVERIFIED |
| 8 同事异路径自动发现与导出 | USER-WAIVED / UNVERIFIED；没有已复核的同事机器结果 |
| 9 PDF 页数/图幅/每页渲染 | UNVERIFIED，没有已复核的输出 PDF |
| 10 文档/Issue 一致 | 候选状态与未验收限制已同步；未查询/修改远程 Issue |
| 11 独立代码/证据审查 | 三项 Important 及候选列表差异均修复并复核；最终实机证据复核未完成，工作区仍含原有无关文件 |

## 审查与剩余事项

只读独立评审发现并复核关闭：选择保存期间重入、带引号 UGII_ROOT_DIR 解析错误、
来源诊断不可追溯。RED/GREEN 与具体文件见
[实施评审交接](../../handoffs/2026-09-08-v2-implementation-review.md)。

r2 已按设计 7.1 修正并同步 Task 5 样例：多个有效候选确认选择后仍可见，显示
根目录与版本，保留仍有效的高亮项；失效时回退到当前安装/首项，候选清空时移除
旧项。独立只读复核通过。两项选定后可见测试先失败再通过；新增选择/格式化
接口先出现 CS0117，再完成实现，全部 UI 状态测试 9/9 通过。实际界面尚未验收。

当前没有待处理的已报告代码评审项。仍缺历史 Task 1–2 原始 RED 证据和上述人工
证据；不再等待这些人工证据来整理交接，但不会将证据缺口或 Tasks 8–10 改记为 PASS。

本轮已同步验收放行决定、计划和交接材料。[本机清单](local-manual-checklist.md)
与 [同事清单](colleague-manual-checklist.md) 保留供未来补证，不要求现在执行。
若未来做真实 NX 验证，源文件保护、每轮哈希和异常停止规则不变。
2026-09-14 后续授权：用户已明确授权本地提交、向其 GitHub 推送并开源。
执行前检查许可、历史和隐私；不将 USER-WAIVED 作为机器兼容性证据。
该授权不包含新建标签、Release、上传候选 ZIP 或修改/关闭验收 Issue。

源码提交前复核（2026-09-14）：完整 Release 测试再次 200/200 通过，只有原有
NU1702；仅暂存 39 个 V2 文件，未包括缓存、August handoff、私有样例或二进制。
暂存差异格式检查与凭据模式扫描通过；28 个既有可达提交的模式扫描仅匹配
检查器的合成用户路径测试。历史 V1 ZIP 与既有 Release 公布哈希相同，7 个条目
不包含 CAD 样例或 Siemens DLL；本轮没有修改旧 Release。
