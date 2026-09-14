# V2 本机人工验收：先做 Task 8

2026-09-14：用户要求不再等待这四项人工验收证据，继续后续工作。本清单保留供
将来补证，不要求现在执行；状态为用户放行、证据未核验，不是实测 PASS。
下面的顺序与停止条件仅在实际执行验收时适用。

当前包仅用于验收，不是正式发布。所有 GUI、缩放设置、断网和截图由本人操作。
先完成本节并交回证据；Task 8 审核通过后才做后面的 DPI/断网矩阵。

## 前置条件与目的

Windows 11 x64、已安装 NX 10.0.0.24；关闭可能编辑样件的其他 NX 会话，确认无
未保存文档。准备制图 PRT 和关联模型（保持原位置和名称）。测试目的：证明干净
解压后的候选包能发现安装、启动正确 Worker、生成完整 PDF，并保持两个源文件不变。
不要把样件放入安装包，也不要在安装包目录内保存截图和日志。

使用本次交付的 NXDrawingPdfExporter-v2-candidate.zip 与 manifest.sha256。
当前使用 2026-09-13 r2 候选：`artifacts/release/v2-candidate-20260913-r2/` 下的 ZIP、
manifest.sha256 和 candidate.sha256；不混用保留的 2026-09-10 或 2026-09-13 首包。
用户已授权将人工验收留到最后，本清单现在无需立即执行。

## 1. 手工准备新目录与导出前哈希

在普通 PowerShell 中执行以下代码。代码不启动 GUI；输入只保存在临时证据目录，
回报时只报告相等/不相等，不把私人路径和哈希贴入聊天。

```powershell
$ErrorActionPreference = 'Stop'
$candidateZip = Read-Host '候选 ZIP 完整路径'
$candidateManifest = Read-Host 'manifest.sha256 完整路径'
$drawingPrt = Read-Host '制图 PRT 完整路径'
$modelPrt = Read-Host '关联模型 PRT 完整路径'
$qaRoot = Join-Path $env:TEMP ('nxpdf-v2-qa-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qaRoot | Out-Null
$cleanPackage = Join-Path $qaRoot 'package'
Expand-Archive -LiteralPath $candidateZip -DestinationPath $cleanPackage
$qaOutput = Join-Path $qaRoot 'output'
New-Item -ItemType Directory -Path $qaOutput | Out-Null
foreach ($line in [IO.File]::ReadAllLines($candidateManifest)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $entry = $line -split '  ', 2
    if ($entry.Count -ne 2) { throw '清单格式错误' }
    $actual = (Get-FileHash -LiteralPath (Join-Path $cleanPackage $entry[1]) -Algorithm SHA256).Hash
    if ($actual -ne $entry[0]) { throw '清单不匹配：停止' }
}
$before = @(
    [pscustomobject]@{Role='drawing';Path=$drawingPrt;Hash=(Get-FileHash -LiteralPath $drawingPrt -Algorithm SHA256).Hash},
    [pscustomobject]@{Role='model';Path=$modelPrt;Hash=(Get-FileHash -LiteralPath $modelPrt -Algorithm SHA256).Hash}
)
$before | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qaRoot 'before.json') -Encoding UTF8
Write-Host '清单已核对，导出前哈希已保存。'
Write-Host ('解压目录：' + $cleanPackage)
Write-Host ('本次输出目录：' + $qaOutput)
Write-Host ('证据目录：' + $qaRoot)
```

预期：无报错，7 个包文件逐项符合清单，两个源文件可读。任一步失败即停止。
不运行候选程序前先核对 ZIP 本身与 candidate.sha256 的哈希一致。

## 2. 人工启动与一次真实导出

1. 记录当前显示器编号、分辨率和 Windows 缩放（本轮建议 100%，保持分辨率不变）。
   截取整个 Windows 显示设置窗口，必须能看见显示器、分辨率和缩放值。
2. 本人从上面新解压目录双击 EXE。等待自动检测，不先点击手动选择。
   预期为 NX 10.0.0.24 已就绪，根目录与本机安装一致。截取完整程序窗口，包括
   标题栏、NX 环境区与底部内容；较小屏幕再保存滚动到底部的补图。
3. 若自动发现失败，停止自动验收，保存完整窗口及错误，并保留（若存在）
   `%LocalAppData%\NxDrawingPdfExporter\diagnostics\run.log`；手动选择仅作为单独兜底
   测试，不把其成功记成自动发现成功。不要创建环境变量或修注册表绕过。
4. 手动多选模式仅选择本次制图 PRT；统一输出目录设置为上述全新 output；选择
   跳过已有 PDF。预检查应只有该源文件、同名 PDF 目标及可运行状态。
   截图包含完整预检查列表与 NX 环境区。
5. 点击开始导出。截图运行状态、NX 状态、禁用的输入/选择按钮和取消按钮。
   不强行退出，不结束 Worker，不接受任何保存源文件提示。
6. 等待最终结果。预期成功、正式 PDF 出现、无临时 PDF 残留。
   截图完整程序窗口及结果区补图，然后用“打开日志”记录本轮运行目录。
7. 本人打开生成 PDF，逐页检查页数、原图幅、白底黑线、无水印、无裁切、顺序；
   与既有参考对照。以 100% 检查细节，并以适合整页检查边缘；每页保存整页画面，
   这些图纸截图只留本机证据目录。无已有 PDF 工具时停止，不安装工具。

为核对实际启动路径，可在第 5 步 Worker 仍运行时，由本人在第二个 PowerShell 执行：

```powershell
$processEvidence = Read-Host '本次证据目录的完整路径'
Get-CimInstance Win32_Process -Filter "Name='run_managed.exe'" |
    Where-Object { $_.CommandLine -like '*NxDrawingPdfExporter.Worker.exe*' } |
    Select-Object ProcessId,ExecutablePath,CommandLine |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $processEvidence 'launcher-process.json') -Encoding UTF8
```

该文件必须非空，ExecutablePath 应是环境区显示根目录下的 UGII\run_managed.exe。
若权限不足或错过进程，不提权、不猜测，把该项标为缺证并返回。

## 3. 结束后检查与回传

确认本轮 Worker 已正常退出后，在原 PowerShell 中执行。即使导出失败也需比较
两个文件的前后哈希；不要用新的 before 覆盖旧记录。

```powershell
$after = foreach ($item in $before) {
    $current = (Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash
    [pscustomobject]@{Role=$item.Role;Equal=($current -eq $item.Hash);Hash=$current}
}
$after | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $qaRoot 'after.json') -Encoding UTF8
if (@($after | Where-Object { -not $_.Equal }).Count) { throw '源文件发生变化：立即停止，不再运行' }
Write-Host '两个源文件前后哈希均相等。'
```

回传：证据目录位置、运行日志目录位置、自动/手动检测结果、NX 版本、两个哈希
是否相等、PDF 页数与逐页检查结果，以及不含图纸内容的程序截图。原始 job/result、
进程记录、PDF 和哈希只在本机核验，不提交 Git，不发送同事或外部服务。
助手将继续核对 RunId、逐文件终态、FatalError、PDF 元数据、源完整性，不能仅凭
退出码或截图判定通过。

## 4. Task 8 通过之后的 DPI 矩阵

使用同一份 ZIP、同一显示器和原生分辨率。每次切换 Windows 缩放后关闭并重新
启动应用；系统若要求注销/重启，由本人操作。不能以 PDF 阅读器或浏览器缩放代替。
每次真实导出重新建立独立 before/after 记录与新输出目录。

| Windows 缩放 | 必存画面（每档单独保存） | 检查 |
|---|---|---|
| 100% | 缩放设置、NX 就绪、手动选择入口/多候选（若存在）、扫描模式、手动文件模式、长统一输出路径、预检查、运行禁用状态、最终结果 | 中文无截断，控件无重叠，按钮可访问，列表伸缩/滚动，Tab 顺序 |
| 125% | 同上，另存 125% 文件夹，不复用 100% 图片 | 同上 |
| 150% | 同上，另存 150% 文件夹，不复用其他档图片 | 同上 |

每个状态需完整应用窗口与当前缩放证明同框；分辨率不够时可附同轮连续的设置
截图，并明确未能同框。窗口完整外框必须可见，底部通过滚动补图。缺少真实多候选
条件时标为 NOT RUN，不修改机器来伪造条件，不用纯状态单测替代画面。

若本机确有多个有效安装：每档补截选择前、确认选择后、切换后的完整 NX 环境区。
确认后下拉框仍可见，条目显示完整根目录与版本；高亮另一项不会自行切换当前
安装，需点“使用所选 NX”。保存期间选择/刷新/开始应禁用，结束后恢复。
这些选择操作无需导出；若另外执行真实导出，仍须每轮 fresh before/after。

## 5. Task 8 通过之后的物理断网

保存工作后，由本人关闭 Wi-Fi/拔网线，截取 Windows 断网状态（遮蔽无关网络名）。
从同一 ZIP 再次干净解压；重新记录两个源文件 before，启动、自动检测、预检查、
导出，截图断网状态与完整就绪窗口同框及最终结果。核对没有联网、更新、认证提示，
保存 result/日志/PDF 与两个 after，哈希相等且证据已保留后由本人恢复网络。
软件许可证若因断网失效，记录失败；不更换许可证或修改 NX 配置规避。

## 停止条件

哈希不匹配、版本不符、错误 NX 路径、缺少 Worker、NX 保存提示、源哈希变化、
PDF 缺失/残缺/页数错误、网络提示、要求安装或管理员权限、任一 DPI 下裁切/重叠，
均停止该项。保留现场证据，不覆盖旧 PDF、不强杀 Worker、不重复运行碰运气。
同事异路径验收使用 colleague-manual-checklist.md 与对应 Codex 提示词，必须
验证同一 ZIP 和 manifest，未经本机门槛通过不外发为已验证版本。
