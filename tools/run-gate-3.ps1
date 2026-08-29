# Gate 3 harness: proves all approved batch modes and failure paths on
# DISPOSABLE COPIES under gitignored artifacts. Every controller-driven
# scenario runs the real ApplicationController: real worker launch through
# run_managed.exe, real NX export, real managed PDF validation and
# transactional publication. Originals are hash-verified before and after.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\run-gate-3.ps1 [-SelfTestOnly]
param([switch]$SelfTestOnly)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$runManaged = 'D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe'
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $repoRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repoRoot '.tools\nuget-http-cache'
$env:DOTNET_ROOT = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'

function Get-Sha256Hex {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-NxFingerprint {
    $targets = @(
        'D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe',
        'D:\Program Files\Siemens\NX 10.0\UGII\managed\NXOpen.dll'
    )
    return $targets | ForEach-Object {
        $item = Get-Item -LiteralPath $_
        [ordered]@{ path = $_; length = $item.Length; lastWriteUtc = $item.LastWriteTimeUtc.ToString('O') }
    }
}

function Find-WorkerExe {
    $worker = Get-ChildItem (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\bin') -Recurse -Filter 'NxDrawingPdfExporter.Worker.exe' |
        Where-Object { $_.FullName -like '*\Debug\*' } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace($worker)) { throw '未找到 Worker。' }
    return $worker
}

function Invoke-DriverScenario {
    param([string]$ScenarioLabel, [string[]]$DriverArgs, [string]$GateRoot)
    $driverExe = Join-Path $repoRoot 'tools\NxDrawingPdfExporter.Gate3Driver\bin\Debug\net10.0-windows\NxDrawingPdfExporter.Gate3Driver.exe'
    $runRoot = Join-Path $GateRoot ('runs-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
    $quoted = $DriverArgs | ForEach-Object { '"' + $_ + '"' }
    $output = & $driverExe @quoted $runRoot 2>$null
    if ($LASTEXITCODE -ne 0) { throw ('驱动退出码非零: ' + $ScenarioLabel) }
    $json = ($output -join "`n") | ConvertFrom-Json
    return $json
}

function Assert-ResultStatus {
    param($ScenarioJson, [int]$Index, [string]$Expected)
    $actual = $ScenarioJson.results[$Index].status
    if ($actual -ne $Expected) {
        throw ('场景结果不符: 第 ' + ($Index + 1) + ' 项期望 ' + $Expected + '，实际 ' + $actual + '（' + $ScenarioJson.results[$Index].message + '）')
    }
}

if ($SelfTestOnly) { return }

Write-Host '=== Gate 3 setup ==='
& $taskDotnet build (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\NxDrawingPdfExporter.Worker.csproj') -c Debug --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Worker 构建失败。' }
& $taskDotnet build (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.Gate3Driver\NxDrawingPdfExporter.Gate3Driver.csproj') -c Debug --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Gate3Driver 构建失败。' }

$workerExe = Find-WorkerExe
$env:NXPDF_WORKER_EXE = $workerExe

$gateRoot = Join-Path $repoRoot ('artifacts\gate-3-fixtures\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $gateRoot -Force | Out-Null

# Disposable fixture copies. Originals are never touched.
$drawingName = 'PRIVATE_SAMPLE_A.prt'
$modelName = 'PRIVATE_SAMPLE_B.prt'
$fixtures = @('pair', 'nested\a', 'nested\b', 'dup1', 'dup2', 'puremodel', 'missingdep')
foreach ($fixture in $fixtures) {
    New-Item -ItemType Directory -Path (Join-Path $gateRoot $fixture) -Force | Out-Null
}
foreach ($fixture in @('pair', 'nested\a', 'nested\b', 'dup1', 'dup2')) {
    Copy-Item (Join-Path $repoRoot ('samples\private\' + $drawingName)) (Join-Path $gateRoot ($fixture + '\' + $drawingName))
    Copy-Item (Join-Path $repoRoot ('samples\private\' + $modelName)) (Join-Path $gateRoot ($fixture + '\' + $modelName))
}
Copy-Item (Join-Path $repoRoot ('samples\private\' + $modelName)) (Join-Path $gateRoot ('puremodel\' + $modelName))
Copy-Item (Join-Path $repoRoot ('samples\private\' + $drawingName)) (Join-Path $gateRoot ('missingdep\' + $drawingName))

$originalDrawingHash = Get-Sha256Hex (Join-Path $repoRoot ('samples\private\' + $drawingName))
$originalModelHash = Get-Sha256Hex (Join-Path $repoRoot ('samples\private\' + $modelName))
$nxFingerprintBefore = Get-NxFingerprint

$pairDrawing = Join-Path $gateRoot ('pair\' + $drawingName)
$pairModel = Join-Path $gateRoot ('pair\' + $modelName)
$pureModel = Join-Path $gateRoot ('puremodel\' + $modelName)
$missingDep = Join-Path $gateRoot ('missingdep\' + $drawingName)
$unifiedDir = Join-Path $gateRoot 'unified-out'
New-Item -ItemType Directory -Path $unifiedDir -Force | Out-Null

$failures = @()

Write-Host '=== S1 手动多选 + 跟随源目录 + 跳过 ==='
$s1 = Invoke-DriverScenario 'S1' @('manual', ($pairDrawing + ';' + $pairModel + ';' + $pureModel), 'beside', '-', 'skip', '.') $gateRoot
Assert-ResultStatus $s1 0 'Success'
Assert-ResultStatus $s1 1 'PureModel'
Assert-ResultStatus $s1 2 'PureModel'
if (-not (Test-Path (Join-Path $gateRoot 'pair\PRIVATE_SAMPLE_A.pdf'))) { throw 'S1 未产出 PDF。' }

Write-Host '=== S2 扫描文件夹（当前层） ==='
$s2 = Invoke-DriverScenario 'S2' @('scan', (Join-Path $gateRoot 'pair'), '0', 'beside', '-', 'skip', '.') $gateRoot
Assert-ResultStatus $s2 0 'SkippedExisting'

Write-Host '=== S3 递归扫描 ==='
$s3 = Invoke-DriverScenario 'S3' @('scan', (Join-Path $gateRoot 'nested'), '1', 'beside', '-', 'skip', '.') $gateRoot
# Recursive scan legitimately discovers the model PRTs too; the worker must
# identify them as pure models. Assert by counts, not enumeration order.
$s3Success = @($s3.results | Where-Object { $_.status -eq 'Success' }).Count
$s3Pure = @($s3.results | Where-Object { $_.status -eq 'PureModel' }).Count
if ($s3.results.Count -ne 4 -or $s3Success -ne 2 -or $s3Pure -ne 2) { throw ('S3 递归扫描结果不符: 共 ' + $s3.results.Count + ' 项，成功 ' + $s3Success + '，纯模型 ' + $s3Pure) }

Write-Host '=== S4 统一输出目录 ==='
$s4 = Invoke-DriverScenario 'S4' @('manual', $pairDrawing, 'unified', $unifiedDir, 'skip', '.') $gateRoot
Assert-ResultStatus $s4 0 'Success'
if (-not (Test-Path (Join-Path $unifiedDir 'PRIVATE_SAMPLE_A.pdf'))) { throw 'S4 统一目录未产出 PDF。' }

Write-Host '=== S5 失败后继续（缺失依赖 + 正常文件） ==='
$s5 = Invoke-DriverScenario 'S5' @('manual', ($missingDep + ';' + $pairDrawing), 'beside', '-', 'skip', '.') $gateRoot
Assert-ResultStatus $s5 0 'Failed'
Assert-ResultStatus $s5 1 'SkippedExisting'
if ([string]::IsNullOrWhiteSpace($s5.results[0].message)) { throw 'S5 失败项缺少用户可读原因。' }
$tempLeftovers = Get-ChildItem (Join-Path $gateRoot 'missingdep') -Filter '*.tmp.pdf' -ErrorAction SilentlyContinue
if ($tempLeftovers) { throw 'S5 失败项残留临时 PDF。' }

Write-Host '=== S6 安全覆盖（预置旧文件） ==='
$overwriteTarget = Join-Path $gateRoot 'pair\PRIVATE_SAMPLE_A.pdf'
$pdfBeforeOverwrite = Get-Sha256Hex $overwriteTarget
$s6 = Invoke-DriverScenario 'S6' @('manual', $pairDrawing, 'beside', '-', 'overwrite', '.') $gateRoot
Assert-ResultStatus $s6 0 'Overwritten'
if ((Get-Sha256Hex $overwriteTarget) -eq $pdfBeforeOverwrite) { throw 'S6 覆盖未发生。' }

Write-Host '=== S7 名称冲突阻止执行 ==='
$dup1 = Join-Path (Join-Path $gateRoot 'dup1') $drawingName
$dup2 = Join-Path (Join-Path $gateRoot 'dup2') $drawingName
$s7 = Invoke-DriverScenario 'S7' @('manual', ($dup1 + ';' + $dup2), 'unified', $unifiedDir, 'skip', '.') $gateRoot
if ($s7.results.Count -lt 2) { throw 'S7 应报告两个冲突项。' }
foreach ($result in $s7.results) {
    if ($result.status -ne 'NameConflict') { throw ('S7 存在非冲突结果: ' + $result.status) }
}
if ($s7.runDirectory) { throw 'S7 冲突不应创建运行目录或启动 Worker。' }

Write-Host '=== S8 覆盖失败保留原文件（锁定目标） ==='
$lockedPath = Join-Path $gateRoot 'pair\PRIVATE_SAMPLE_A.pdf'
$lockedBefore = Get-Sha256Hex $lockedPath
$lockStream = [System.IO.File]::Open($lockedPath, 'Open', 'Read', 'None')
try {
    $s8 = Invoke-DriverScenario 'S8' @('manual', $pairDrawing, 'beside', '-', 'overwrite', '.') $gateRoot
} finally {
    $lockStream.Close()
}
Assert-ResultStatus $s8 0 'Failed'
if ((Get-Sha256Hex $lockedPath) -ne $lockedBefore) { throw 'S8 覆盖失败后原文件字节发生变化。' }
$tempLeftovers = Get-ChildItem (Join-Path $gateRoot 'pair') -Filter '*.tmp.pdf' -ErrorAction SilentlyContinue
if ($tempLeftovers) { throw 'S8 残留临时 PDF。' }
$backupsLeft = Get-ChildItem (Join-Path $gateRoot 'pair') -Filter '*.bak.pdf' -ErrorAction SilentlyContinue
if ($backupsLeft) { throw 'S8 残留备份文件。' }

Write-Host '=== S9 安全取消（第一个文件前） ==='
$cancelRunDir = Join-Path $gateRoot ('cancel-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $cancelRunDir -Force | Out-Null
$cancelJob = [ordered]@{
    ProtocolVersion      = '1'
    RunId                = 'cancel-run'
    Items                = @([ordered]@{
        SourcePath          = $pairDrawing
        FinalOutputPath     = Join-Path $cancelRunDir 'never.pdf'
        WorkerTempOutputPath = (Join-Path $cancelRunDir '.never.tmp.pdf')
    })
    OutputMode           = 0
    ExistingPdfPolicy    = 0
    ResultPath           = (Join-Path $cancelRunDir 'result.json')
    CancellationFlagPath = (Join-Path $cancelRunDir 'cancel.flag')
}
Set-Content -LiteralPath (Join-Path $cancelRunDir 'cancel.flag') -Value 'cancel'
$cancelJobPath = Join-Path $cancelRunDir 'job.json'
$cancelJob | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $cancelJobPath -Encoding UTF8
$cancelProcess = Start-Process -FilePath $runManaged -ArgumentList @('"' + $workerExe + '"', '--run-job', '"' + $cancelJobPath + '"') -WorkingDirectory $cancelRunDir -RedirectStandardOutput (Join-Path $cancelRunDir 'stdout.txt') -RedirectStandardError (Join-Path $cancelRunDir 'stderr.txt') -PassThru -Wait
$cancelResult = Get-Content (Join-Path $cancelRunDir 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $cancelResult.Cancelled) { throw 'S9 结果未报告取消。' }
if ($cancelResult.Files.Count -lt 1 -or [int]$cancelResult.Files[0].Status -ne 6) { throw ('S9 文件结果不是 Cancelled: ' + $cancelResult.Files[0].Status) }
if (Test-Path (Join-Path $cancelRunDir 'never.pdf')) { throw 'S9 取消后不应有输出。' }

Write-Host '=== 源文件与系统保护复核 ==='
$afterDrawingHash = Get-Sha256Hex (Join-Path $repoRoot ('samples\private\' + $drawingName))
$afterModelHash = Get-Sha256Hex (Join-Path $repoRoot ('samples\private\' + $modelName))
if ($afterDrawingHash -ne $originalDrawingHash -or $afterModelHash -ne $originalModelHash) {
    throw '原始样例哈希变化：硬失败。'
}
$nxFingerprintAfter = Get-NxFingerprint
for ($i = 0; $i -lt $nxFingerprintBefore.Count; $i++) {
    if ($nxFingerprintBefore[$i].length -ne $nxFingerprintAfter[$i].length -or
        $nxFingerprintBefore[$i].lastWriteUtc -ne $nxFingerprintAfter[$i].lastWriteUtc) {
        throw 'NX 安装文件指纹变化：硬失败。'
    }
}

$saveHits = & rg -n "\.Save\(|SaveAs\(|SaveComponents|PartSave" (Join-Path $repoRoot 'src') 2>$null
if ($saveHits) { throw '生产代码出现 NX 保存 API 调用。' }

Write-Host 'GATE 3: PASS (全部场景通过；原始样例前后哈希一致；NX 安装指纹未变；无保存 API 调用)'
Write-Host ('GATE ROOT: ' + $gateRoot)
