# Gate 3 harness: proves all approved batch modes and failure paths on
# DISPOSABLE COPIES under gitignored artifacts. Every controller-driven
# scenario runs the real ApplicationController: real worker launch through
# run_managed.exe, real NX export, real managed PDF validation and
# transactional publication. Originals are hash-verified before and after.
#
# CR-09: the private drawing/model pair is DISCOVERED from the gitignored
# samples\private directory (or provided via NXPDF_PRIVATE_DRAWING /
# NXPDF_PRIVATE_MODEL); real sample names are never written into tracked
# files. Before any NX work the harness verifies that no tracked file still
# contains the discovered private name tokens.
#
# EV coverage: S5 proves failure followed by a real later export (EV-01),
# S10 proves cancellation observed at the next-file boundary (EV-02),
# S11 proves a real no-valid-sheet PRT is skipped (EV-03).
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

function Find-PdfGateExe {
    $gate = Get-ChildItem (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.PdfGate\bin') -Recurse -Filter 'NxDrawingPdfExporter.PdfGate.exe' |
        Where-Object { $_.FullName -like '*\Debug\*' } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if ([string]::IsNullOrWhiteSpace($gate)) { throw '未找到 PdfGate。' }
    return $gate
}

function Find-PrivatePair {
    # 从 gitignored 的 samples\private 发现图纸/模型对；也可用环境变量显式
    # 指定。真实样例名只存在于本进程内存与控制台，绝不写入受跟踪文件。
    $overrideDrawing = $env:NXPDF_PRIVATE_DRAWING
    $overrideModel = $env:NXPDF_PRIVATE_MODEL
    if ($overrideDrawing -and $overrideModel) {
        return @($overrideDrawing, $overrideModel)
    }

    $privateDir = Join-Path $repoRoot 'samples\private'
    $parts = @(Get-ChildItem $privateDir -Filter '*.prt' -File)
    $drawings = @($parts | Where-Object { $_.Name -like 'DWG_*.prt' })
    $models = @($parts | Where-Object { $_.Name -notlike 'DWG_*.prt' })
    if ($drawings.Count -ne 1 -or $models.Count -ne 1) {
        throw '无法在 samples\private 中唯一发现图纸/模型对（要求恰好一个 DWG_*.prt 与一个非 DWG_*.prt 模型）。请用 NXPDF_PRIVATE_DRAWING / NXPDF_PRIVATE_MODEL 显式指定。'
    }

    return @($drawings[0].FullName, $models[0].FullName)
}

function Assert-NoPrivateNamesInTrackedFiles {
    param([string[]]$Tokens)
    Push-Location $repoRoot
    try {
        foreach ($token in $Tokens) {
            if ([string]::IsNullOrWhiteSpace($token)) { continue }
            # 用户批准的设计规格文本是唯一豁免范围（CR-09 记录在案）。
            $hits = & git grep -I -F -l -e $token -- '.' ':(exclude)docs/superpowers/specs'
            if ($LASTEXITCODE -eq 0 -and $hits) {
                throw ('受跟踪文件包含私有样例名，违反 CR-09: ' + ($hits -join ', '))
            }
        }
    }
    finally {
        Pop-Location
    }
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

function Assert-PdfValid {
    param([string]$PdfGateExe, [string]$PdfPath, [int]$ExpectedPages, [string]$Label)
    if (-not (Test-Path -LiteralPath $PdfPath)) { throw ($Label + ' 未产出 PDF: ' + $PdfPath) }
    $inspectionPath = $PdfPath + '.inspect.json'
    & $PdfGateExe inspect $PdfPath $inspectionPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw ($Label + ' PDF 检查失败。') }
    $inspection = Get-Content -LiteralPath $inspectionPath -Raw -Encoding UTF8 | ConvertFrom-Json
    Remove-Item -LiteralPath $inspectionPath -Force
    if (-not $inspection.HasPdfHeader) { throw ($Label + ' PDF 缺少文件头。') }
    if ([int]$inspection.PageCount -ne $ExpectedPages) {
        throw ($Label + ' PDF 页数 ' + $inspection.PageCount + ' 与预期 ' + $ExpectedPages + ' 不一致。')
    }
}

function StringAssertContainsCheck {
    param([string]$Actual, [string]$Expected, [string]$Label)
    if (-not $Actual.Contains($Expected)) {
        throw ($Label + '：文本"' + $Actual + '"不包含"' + $Expected + '"。')
    }
}

if ($SelfTestOnly) { return }

# 逐场景源文件保护：每次真实 NX 运行前后对实际被打开的图纸及关联模型
# （各目录中的临时副本）逐个记录 SHA-256，任何变化即硬失败；结构化报告
# 写入 gitignored 的 gateRoot（二审发现 3）。
$sourceHashReport = New-Object System.Collections.Generic.List[object]

function Get-DirPrtHashes {
    param([string[]]$Dirs)
    $map = @{}
    foreach ($dir in $Dirs) {
        Get-ChildItem $dir -Recurse -Filter '*.prt' -File | ForEach-Object {
            $map[$_.FullName] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
    return $map
}

function Invoke-ProtectedScenario {
    param([string]$Label, [string[]]$WatchDirs, [scriptblock]$Body)
    $before = Get-DirPrtHashes -Dirs $WatchDirs
    & $Body
    $after = Get-DirPrtHashes -Dirs $WatchDirs
    foreach ($key in $before.Keys) {
        $afterHash = $null
        if ($after.ContainsKey($key)) { $afterHash = $after[$key] }
        $ok = ($afterHash -eq $before[$key])
        $sourceHashReport.Add([ordered]@{
            scenario = $Label
            file     = $key
            before   = $before[$key]
            after    = $afterHash
            ok       = $ok
        })
        if (-not $ok) { throw ('场景 ' + $Label + ' 源文件哈希变化：' + $key + '（硬失败）') }
    }

    foreach ($key in $after.Keys) {
        if (-not $before.ContainsKey($key)) { throw ('场景 ' + $Label + ' 出现了未预期的 PRT：' + $key) }
    }

    Write-Host ('  （' + $Label + ' 源哈希保护：' + $before.Count + ' 个 PRT 前后一致）')
}

Write-Host '=== Gate 3 setup ==='
& $taskDotnet build (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\NxDrawingPdfExporter.Worker.csproj') -c Debug --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Worker 构建失败。' }
& $taskDotnet build (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.Gate3Driver\NxDrawingPdfExporter.Gate3Driver.csproj') -c Debug --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Gate3Driver 构建失败。' }
& $taskDotnet build (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.PdfGate\NxDrawingPdfExporter.PdfGate.csproj') -c Debug --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'PdfGate 构建失败。' }
& $taskDotnet build (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.FixtureBuilder\NxDrawingPdfExporter.FixtureBuilder.csproj') -c Debug --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'FixtureBuilder 构建失败。' }

$workerExe = Find-WorkerExe
$pdfGateExe = Find-PdfGateExe
$env:NXPDF_WORKER_EXE = $workerExe

$pair = Find-PrivatePair
$originalDrawingPath = $pair[0]
$originalModelPath = $pair[1]
$drawingName = Split-Path -Leaf $originalDrawingPath
$modelName = Split-Path -Leaf $originalModelPath
$drawingBase = [System.IO.Path]::GetFileNameWithoutExtension($drawingName)
Write-Host ('私有图纸/模型对已发现（名称不写入任何受跟踪文件）。')

Write-Host '=== CR-09 隐私扫描：受跟踪文件不得包含真实样例名 ==='
Assert-NoPrivateNamesInTrackedFiles -Tokens @($drawingBase, ([System.IO.Path]::GetFileNameWithoutExtension($modelName)))

$gateRoot = Join-Path $repoRoot ('artifacts\gate-3-fixtures\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $gateRoot -Force | Out-Null

# Disposable fixture copies. Originals are never touched.
$fixtures = @('pair', 'nested\a', 'nested\b', 'dup1', 'dup2', 'puremodel', 'missingdep', 'fresh', 'fresh3', 's12a', 's12b')
foreach ($fixture in $fixtures) {
    New-Item -ItemType Directory -Path (Join-Path $gateRoot $fixture) -Force | Out-Null
}
foreach ($fixture in @('pair', 'nested\a', 'nested\b', 'dup1', 'dup2', 'fresh', 'fresh3', 's12a', 's12b')) {
    Copy-Item $originalDrawingPath (Join-Path $gateRoot ($fixture + '\' + $drawingName))
    Copy-Item $originalModelPath (Join-Path $gateRoot ($fixture + '\' + $modelName))
}
Copy-Item $originalModelPath (Join-Path $gateRoot ('puremodel\' + $modelName))
Copy-Item $originalDrawingPath (Join-Path $gateRoot ('missingdep\' + $drawingName))

$originalDrawingHash = Get-Sha256Hex $originalDrawingPath
$originalModelHash = Get-Sha256Hex $originalModelPath
$nxFingerprintBefore = Get-NxFingerprint

$pairDrawing = Join-Path $gateRoot ('pair\' + $drawingName)
$pairModel = Join-Path $gateRoot ('pair\' + $modelName)
$pureModel = Join-Path $gateRoot ('puremodel\' + $modelName)
$missingDep = Join-Path $gateRoot ('missingdep\' + $drawingName)
$freshDrawing = Join-Path $gateRoot ('fresh\' + $drawingName)
$fresh3Drawing = Join-Path $gateRoot ('fresh3\' + $drawingName)
$unifiedDir = Join-Path $gateRoot 'unified-out'
New-Item -ItemType Directory -Path $unifiedDir -Force | Out-Null

Write-Host '=== S1 手动多选 + 跟随源目录 + 跳过 ==='
Invoke-ProtectedScenario 'S1' @((Join-Path $gateRoot 'pair'), (Join-Path $gateRoot 'puremodel')) {
    $s1 = Invoke-DriverScenario 'S1' @('manual', ($pairDrawing + ';' + $pairModel + ';' + $pureModel), 'beside', '-', 'skip', '.') $gateRoot
    Assert-ResultStatus $s1 0 'Success'
    Assert-ResultStatus $s1 1 'PureModel'
    Assert-ResultStatus $s1 2 'PureModel'
    Assert-PdfValid -PdfGateExe $pdfGateExe -PdfPath (Join-Path $gateRoot ('pair\' + $drawingBase + '.pdf')) -ExpectedPages 1 -Label 'S1'
}

Write-Host '=== S2 扫描文件夹（当前层） ==='
Invoke-ProtectedScenario 'S2' @((Join-Path $gateRoot 'pair')) {
    $s2 = Invoke-DriverScenario 'S2' @('scan', (Join-Path $gateRoot 'pair'), '0', 'beside', '-', 'skip', '.') $gateRoot
    Assert-ResultStatus $s2 0 'SkippedExisting'
}

Write-Host '=== S3 递归扫描 ==='
Invoke-ProtectedScenario 'S3' @((Join-Path $gateRoot 'nested')) {
    $s3 = Invoke-DriverScenario 'S3' @('scan', (Join-Path $gateRoot 'nested'), '1', 'beside', '-', 'skip', '.') $gateRoot
    # Recursive scan legitimately discovers the model PRTs too; the worker must
    # identify them as pure models. Assert by counts, not enumeration order.
    $s3Success = @($s3.results | Where-Object { $_.status -eq 'Success' }).Count
    $s3Pure = @($s3.results | Where-Object { $_.status -eq 'PureModel' }).Count
    if ($s3.results.Count -ne 4 -or $s3Success -ne 2 -or $s3Pure -ne 2) { throw ('S3 递归扫描结果不符: 共 ' + $s3.results.Count + ' 项，成功 ' + $s3Success + '，纯模型 ' + $s3Pure) }
}

Write-Host '=== S4 统一输出目录 ==='
Invoke-ProtectedScenario 'S4' @((Join-Path $gateRoot 'pair')) {
    $s4 = Invoke-DriverScenario 'S4' @('manual', $pairDrawing, 'unified', $unifiedDir, 'skip', '.') $gateRoot
    Assert-ResultStatus $s4 0 'Success'
    Assert-PdfValid -PdfGateExe $pdfGateExe -PdfPath (Join-Path $unifiedDir ($drawingBase + '.pdf')) -ExpectedPages 1 -Label 'S4'
}

Write-Host '=== S5 失败后继续到真实成功导出（EV-01） ==='
# fresh 目标没有任何预存在 PDF：第二个条目必须是真实的 NX 导出成功。
Invoke-ProtectedScenario 'S5' @((Join-Path $gateRoot 'missingdep'), (Join-Path $gateRoot 'fresh')) {
    $s5 = Invoke-DriverScenario 'S5' @('manual', ($missingDep + ';' + $freshDrawing), 'beside', '-', 'skip', '.') $gateRoot
    Assert-ResultStatus $s5 0 'Failed'
    Assert-ResultStatus $s5 1 'Success'
    if ([string]::IsNullOrWhiteSpace($s5.results[0].message)) { throw 'S5 失败项缺少用户可读原因。' }
    $tempLeftovers = Get-ChildItem (Join-Path $gateRoot 'missingdep') -Filter '*.tmp.pdf' -ErrorAction SilentlyContinue
    if ($tempLeftovers) { throw 'S5 失败项残留临时 PDF。' }
    Assert-PdfValid -PdfGateExe $pdfGateExe -PdfPath (Join-Path $gateRoot ('fresh\' + $drawingBase + '.pdf')) -ExpectedPages 1 -Label 'S5'
}

Write-Host '=== S6 安全覆盖（预置旧文件） ==='
Invoke-ProtectedScenario 'S6' @((Join-Path $gateRoot 'pair')) {
    $overwriteTarget = Join-Path $gateRoot ('pair\' + $drawingBase + '.pdf')
    $pdfBeforeOverwrite = Get-Sha256Hex $overwriteTarget
    $s6 = Invoke-DriverScenario 'S6' @('manual', $pairDrawing, 'beside', '-', 'overwrite', '.') $gateRoot
    Assert-ResultStatus $s6 0 'Overwritten'
    if ((Get-Sha256Hex $overwriteTarget) -eq $pdfBeforeOverwrite) { throw 'S6 覆盖未发生。' }
}

Write-Host '=== S7 名称冲突阻止执行 ==='
Invoke-ProtectedScenario 'S7' @((Join-Path $gateRoot 'dup1'), (Join-Path $gateRoot 'dup2')) {
    $dup1 = Join-Path (Join-Path $gateRoot 'dup1') $drawingName
    $dup2 = Join-Path (Join-Path $gateRoot 'dup2') $drawingName
    $s7 = Invoke-DriverScenario 'S7' @('manual', ($dup1 + ';' + $dup2), 'unified', $unifiedDir, 'skip', '.') $gateRoot
    if ($s7.results.Count -lt 2) { throw 'S7 应报告两个冲突项。' }
    foreach ($result in $s7.results) {
        if ($result.status -ne 'NameConflict') { throw ('S7 存在非冲突结果: ' + $result.status) }
    }
    if ($s7.runDirectory) { throw 'S7 冲突不应创建运行目录或启动 Worker。' }
}

Write-Host '=== S8 覆盖失败保留原文件（锁定目标） ==='
Invoke-ProtectedScenario 'S8' @((Join-Path $gateRoot 'pair')) {
    $lockedPath = Join-Path $gateRoot ('pair\' + $drawingBase + '.pdf')
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
}

Write-Host '=== S9 安全取消（第一个文件前） ==='
Invoke-ProtectedScenario 'S9' @((Join-Path $gateRoot 'pair')) {
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
}

Write-Host '=== S10 文件边界取消：条目 1 期间请求取消（EV-02） ==='
# 两个就绪条目。观察者轮询条目 1 的临时 PDF：文件一出现（绘图进行中）立即
# 写入取消标志。取消只在文件边界生效：条目 1 原子完成，条目 2 不得开始。
$s10Dir = Join-Path $gateRoot ('boundary-cancel-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $s10Dir 'a') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $s10Dir 'b') -Force | Out-Null
Copy-Item $originalDrawingPath (Join-Path $s10Dir ('a\' + $drawingName))
Copy-Item $originalModelPath (Join-Path $s10Dir ('a\' + $modelName))
Copy-Item $originalDrawingPath (Join-Path $s10Dir ('b\' + $drawingName))
Copy-Item $originalModelPath (Join-Path $s10Dir ('b\' + $modelName))
Invoke-ProtectedScenario 'S10' @((Join-Path $s10Dir 'a'), (Join-Path $s10Dir 'b')) {
    $s10TempA = Join-Path $s10Dir ('a\.boundary.tmp.pdf')
    $s10TempB = Join-Path $s10Dir ('b\.boundary.tmp.pdf')
    $s10Job = [ordered]@{
        ProtocolVersion      = '1'
        RunId                = 'boundary-cancel-run'
        Items                = @(
            [ordered]@{
                SourcePath           = (Join-Path $s10Dir ('a\' + $drawingName))
                FinalOutputPath      = (Join-Path $s10Dir ('a\' + $drawingBase + '.pdf'))
                WorkerTempOutputPath = $s10TempA
            },
            [ordered]@{
                SourcePath           = (Join-Path $s10Dir ('b\' + $drawingName))
                FinalOutputPath      = (Join-Path $s10Dir ('b\' + $drawingBase + '.pdf'))
                WorkerTempOutputPath = $s10TempB
            }
        )
        OutputMode           = 0
        ExistingPdfPolicy    = 0
        ResultPath           = (Join-Path $s10Dir 'result.json')
        CancellationFlagPath = (Join-Path $s10Dir 'cancel.flag')
    }
    $s10JobPath = Join-Path $s10Dir 'job.json'
    $s10Job | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $s10JobPath -Encoding UTF8
    $s10Process = Start-Process -FilePath $runManaged -ArgumentList @('"' + $workerExe + '"', '--run-job', '"' + $s10JobPath + '"') -WorkingDirectory $s10Dir -RedirectStandardOutput (Join-Path $s10Dir 'stdout.txt') -RedirectStandardError (Join-Path $s10Dir 'stderr.txt') -PassThru
    $flagWritten = $false
    while (-not $s10Process.HasExited) {
        if (-not $flagWritten -and (Test-Path -LiteralPath $s10TempA)) {
            Set-Content -LiteralPath (Join-Path $s10Dir 'cancel.flag') -Value 'cancel'
            $flagWritten = $true
            Write-Host '  （条目 1 临时 PDF 出现，取消标志已写入。）'
        }
        Start-Sleep -Milliseconds 10
    }
    if (-not $flagWritten) { throw 'S10 未能在条目 1 期间观察到临时 PDF，无法证明边界取消。' }
    if (-not $s10Process.WaitForExit(60000)) { throw 'S10 Worker 未在时限内退出。' }
    $s10Result = Get-Content (Join-Path $s10Dir 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $s10Result.Cancelled) { throw 'S10 结果未报告取消。' }
    if ([int]$s10Result.Files[0].Status -ne 0) { throw ('S10 条目 1 不是 Success: ' + $s10Result.Files[0].Status) }
    if ([int]$s10Result.Files[1].Status -ne 6) { throw ('S10 条目 2 不是 Cancelled: ' + $s10Result.Files[1].Status) }
    Assert-PdfValid -PdfGateExe $pdfGateExe -PdfPath $s10TempA -ExpectedPages 1 -Label 'S10 条目 1 临时'
    if (Test-Path -LiteralPath $s10TempB) { throw 'S10 条目 2 不应有临时 PDF。' }
    if (Test-Path (Join-Path $s10Dir ('a\' + $drawingBase + '.pdf'))) { throw 'S10 条目 1 不应发布最终 PDF（发布属于 GUI 职责）。' }
    if (Test-Path (Join-Path $s10Dir ('b\' + $drawingBase + '.pdf'))) { throw 'S10 条目 2 不应有任何输出。' }
}

Write-Host '=== S11 无有效图纸页的真实 PRT（EV-03） ==='
# FixtureBuilder 通过 run_managed 创建"有图纸页但零制图视图"的全新合成
# 零件；Worker 必须报告 NoValidSheets 且继续导出后续有效图纸。
$fixtureBuilderExe = Get-ChildItem (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.FixtureBuilder\bin') -Recurse -Filter 'NxDrawingPdfExporter.FixtureBuilder.exe' |
    Where-Object { $_.FullName -like '*\Debug\*' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($fixtureBuilderExe)) { throw '未找到 FixtureBuilder。' }
$noviewDir = Join-Path $gateRoot 'noviewsheet'
New-Item -ItemType Directory -Path $noviewDir -Force | Out-Null
$noviewPrt = Join-Path $noviewDir 'noviewsheet.prt'
$fbOut = Join-Path $noviewDir 'builder-stdout.txt'
$fbErr = Join-Path $noviewDir 'builder-stderr.txt'
$fbProcess = Start-Process -FilePath $runManaged -ArgumentList @('"' + $fixtureBuilderExe + '"', '"' + $noviewPrt + '"') -WorkingDirectory $noviewDir -RedirectStandardOutput $fbOut -RedirectStandardError $fbErr -PassThru -Wait
if (-not (Test-Path -LiteralPath $noviewPrt) -or (Get-Item -LiteralPath $noviewPrt).Length -eq 0) {
    throw ('EV-03 夹具创建失败（run_managed 退出码不可靠，以产物为准）。详见 ' + $fbErr)
}
Invoke-ProtectedScenario 'S11' @($noviewDir, (Join-Path $gateRoot 'fresh3')) {
    $s11 = Invoke-DriverScenario 'S11' @('manual', ($noviewPrt + ';' + $fresh3Drawing), 'beside', '-', 'skip', '.') $gateRoot
    Assert-ResultStatus $s11 0 'NoValidSheets'
    Assert-ResultStatus $s11 1 'Success'
    if (Test-Path (Join-Path $noviewDir 'noviewsheet.pdf')) { throw 'S11 无有效页 PRT 不应产出 PDF。' }
    $tempLeftovers = Get-ChildItem $noviewDir -Filter '*.tmp.pdf' -ErrorAction SilentlyContinue
    if ($tempLeftovers) { throw 'S11 无有效页 PRT 残留临时 PDF。' }
    Assert-PdfValid -PdfGateExe $pdfGateExe -PdfPath (Join-Path $gateRoot ('fresh3\' + $drawingBase + '.pdf')) -ExpectedPages 1 -Label 'S11'
}

Write-Host '=== S12 端到端文件边界取消（二审发现 1，真实 ApplicationController） ==='
# 两个就绪条目。驱动器在条目 1 导出进行中（任一运行临时 PDF 一出现）通过
# 真实 controller.Cancel() 请求取消：Worker 在文件边界取消条目 2 并返回
# [Success, Cancelled]；GUI 发布循环必须照常发布已完成的条目 1。
$s12DirA = Join-Path $gateRoot 's12a'
$s12DirB = Join-Path $gateRoot 's12b'
Invoke-ProtectedScenario 'S12' @($s12DirA, $s12DirB) {
    $s12 = Invoke-DriverScenario 'S12' @('cancel', ($s12DirA + '\' + $drawingName + ';' + $s12DirB + '\' + $drawingName), 'beside', '-', 'skip', '.') $gateRoot
    Assert-ResultStatus $s12 0 'Success'
    Assert-ResultStatus $s12 1 'Cancelled'
    Assert-PdfValid -PdfGateExe $pdfGateExe -PdfPath (Join-Path $s12DirA ($drawingBase + '.pdf')) -ExpectedPages 1 -Label 'S12 条目 1'
    if (Test-Path (Join-Path $s12DirB ($drawingBase + '.pdf'))) { throw 'S12 条目 2 不应有任何输出。' }
    $tempLeftovers = Get-ChildItem @($s12DirA, $s12DirB) -Filter '*.tmp.pdf' -Recurse -ErrorAction SilentlyContinue
    if ($tempLeftovers) { throw 'S12 残留临时 PDF。' }
    StringAssertContainsCheck $s12.progress '取消' 'S12 进度文本应说明取消状态'
}

Write-Host '=== 源文件与系统保护复核 ==='
# 二审发现 3：逐场景哈希报告落盘（gitignored，仅证据用途）。
$hashReportPath = Join-Path $gateRoot 'source-hash-report.json'
$sourceHashReport | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $hashReportPath -Encoding UTF8
$failedEntries = @($sourceHashReport | Where-Object { -not $_.ok })
if ($failedEntries.Count -gt 0) { throw ('逐场景哈希报告存在失败条目: ' + $failedEntries.Count) }
Write-Host ('逐场景源哈希报告: ' + $hashReportPath + '（' + $sourceHashReport.Count + ' 条，全部一致）')

$afterDrawingHash = Get-Sha256Hex $originalDrawingPath
$afterModelHash = Get-Sha256Hex $originalModelPath
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

Write-Host 'GATE 3: PASS (全部场景通过；原始样例前后哈希一致；NX 安装指纹未变；无保存 API 调用；受跟踪文件无私有样例名)'
Write-Host ('GATE ROOT: ' + $gateRoot)
