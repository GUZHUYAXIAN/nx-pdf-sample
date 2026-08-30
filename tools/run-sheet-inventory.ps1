param(
    [string]$DrawingPrt = '',

    [string]$AssociatedModelPrt = '',

    [string]$Configuration = 'Debug',

    [int]$TimeoutSeconds = 900,

    # Loads only the helper functions so tools/test-run-sheet-inventory.ps1
    # can exercise the failure paths without launching NX.
    [switch]$SelfTestOnly
)

$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Test-PositiveNumber {
    param($Value)
    if ($null -eq $Value) { return $false }
    if ($Value -isnot [double] -and $Value -isnot [single] -and $Value -isnot [int] -and $Value -isnot [long] -and $Value -isnot [decimal]) { return $false }
    $asDouble = [double]$Value
    return ($asDouble -gt 0 -and -not [double]::IsNaN($asDouble) -and -not [double]::IsInfinity($asDouble))
}

# Captures SHA-256 for both source PRTs. A per-file hashing failure is
# recorded as an explicit error marker so post-run capture can never be
# skipped silently; the caller turns any marker into a hard failure.
function Write-HashSnapshot {
    param([string]$DrawingPath, [string]$ModelPath, [string]$RunDir, [string]$Phase)
    $snapshot = @{}
    foreach ($entry in @(@('drawing', $DrawingPath), @('model', $ModelPath))) {
        try {
            $snapshot[$entry[0]] = Get-Sha256Hex -Path $entry[1]
        } catch {
            $snapshot[$entry[0]] = '<hash-capture-error: ' + $_.Exception.Message.Replace('"', "'").Replace("`r", ' ').Replace("`n", ' ') + '>'
        }
    }

    $snapshot | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $RunDir ('source-hashes-' + $Phase + '.json')) -Encoding UTF8
    return $snapshot
}

# Terminates only the process object this script started and waits for exit.
# Returns $null on success or a problem description that becomes a failure.
function Stop-LaunchedProcess {
    param([System.Diagnostics.Process]$Process)
    $stopError = $null
    if (-not $Process.HasExited) {
        try {
            $Process.Kill()
        } catch {
            $stopError = '终止进程失败: ' + $_.Exception.Message
        }
    }

    if (-not $Process.WaitForExit(120000)) {
        $stopError = '终止后进程仍未退出。'
    }

    return $stopError
}

# Independent structural validation of the inventory report file. Returns
# $null when the report is present, nonempty, parseable, structurally
# complete, successful, and carries usable sheet facts; otherwise a reason.
# Exit codes and file existence alone are never treated as success evidence.
function Get-InventoryReportIssue {
    param([string]$ReportPath)
    if (-not (Test-Path -LiteralPath $ReportPath)) { return '清点报告文件不存在。' }
    $file = Get-Item -LiteralPath $ReportPath
    if ($file.Length -le 0) { return '清点报告为空文件。' }

    try {
        $report = Get-Content -LiteralPath $ReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        return '清点报告不是可解析的 JSON。'
    }

    if ($null -eq $report) { return '清点报告 JSON 为空。' }
    if ($report.Success -ne $true) { return '清点报告未报告成功。' }
    if (-not [string]::IsNullOrWhiteSpace($report.Failure)) { return '成功报告携带失败原因。' }
    if ($null -eq $report.LoadDiagnostics -or $report.LoadDiagnostics -isnot [Array]) { return '缺少加载诊断数组。' }
    if ($null -eq $report.Sheets -or $report.Sheets -isnot [Array]) { return '缺少图纸页数组。' }
    if ($report.Sheets.Count -lt 1) { return '成功报告未包含任何图纸页事实。' }

    $seenTokens = @{}
    $exportOrderIndices = @()
    for ($index = 0; $index -lt $report.Sheets.Count; $index++) {
        $sheet = $report.Sheets[$index]
        if ($null -eq $sheet) { return ('图纸页 ' + $index + ' 为空。') }
        if ($sheet.NativeIndex -ne $index) { return ('图纸页 ' + $index + ' 的原生枚举索引与数组位置不一致。') }
        if ($null -eq $sheet.ExportOrderIndex -or $sheet.ExportOrderIndex -ne $index) { return ('图纸页 ' + $index + ' 的导出顺序索引与数组位置不一致。') }
        if ($exportOrderIndices -ccontains $sheet.ExportOrderIndex) { return ('图纸页 ' + $index + ' 导出顺序索引重复。') }
        $exportOrderIndices += [int]$sheet.ExportOrderIndex
        if ($null -eq $sheet.DraftingViewCount -or $sheet.DraftingViewCount -lt 0) { return ('图纸页 ' + $index + ' 制图视图数量无效。') }
        if (-not (Test-PositiveNumber $sheet.Length) -or -not (Test-PositiveNumber $sheet.Height)) { return ('图纸页 ' + $index + ' 图幅尺寸无效。') }
        if ($null -eq $sheet.Units -or $sheet.Units -isnot [string] -or $sheet.Units.Length -eq 0) { return ('图纸页 ' + $index + ' 缺少单位。') }
        if ([string]::IsNullOrEmpty($sheet.NameToken)) { return ('图纸页 ' + $index + ' 缺少名称令牌。') }
        if ($sheet.NameToken -cnotmatch '^[0-9a-f]{16}$') { return ('图纸页 ' + $index + ' 名称令牌无效。') }
        if ($seenTokens.ContainsKey($sheet.NameToken)) { return ('图纸页 ' + $index + ' 名称令牌重复。') }
        $seenTokens[$sheet.NameToken] = $true
    }

    return $null
}

if ($SelfTestOnly) { return }

if ([string]::IsNullOrWhiteSpace($DrawingPrt) -or [string]::IsNullOrWhiteSpace($AssociatedModelPrt)) {
    throw '必须提供 -DrawingPrt 与 -AssociatedModelPrt（仅装载函数时请使用 -SelfTestOnly）。'
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$runManaged = 'D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe'
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $repoRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repoRoot '.tools\nuget-http-cache'

$drawingPath = [System.IO.Path]::GetFullPath($DrawingPrt)
$modelPath = [System.IO.Path]::GetFullPath($AssociatedModelPrt)
foreach ($path in @($drawingPath, $modelPath, $runManaged)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "缺少必要本地文件。"
    }
}

& $taskDotnet build (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\NxDrawingPdfExporter.Worker.csproj') -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Worker 构建失败。' }

$workerExe = Get-ChildItem (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\bin') -Recurse -Filter 'NxDrawingPdfExporter.Worker.exe' |
    Where-Object { $_.FullName -like "*\$Configuration\*" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($workerExe)) { throw '未找到 Worker 可执行文件。' }

$runDir = Join-Path $repoRoot ('artifacts\gate-2\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$hashBefore = Write-HashSnapshot -DrawingPath $drawingPath -ModelPath $modelPath -RunDir $runDir -Phase 'before'

$reportPath = Join-Path $runDir 'sheet-inventory.json'
$stdoutPath = Join-Path $runDir 'stdout.txt'
$stderrPath = Join-Path $runDir 'stderr.txt'
$quotedWorker = '"' + $workerExe + '"'
$quotedDrawing = '"' + $drawingPath + '"'
$quotedReport = '"' + $reportPath + '"'
$process = Start-Process -FilePath $runManaged -ArgumentList @($quotedWorker, '--inventory', $quotedDrawing, $quotedReport) -WorkingDirectory $runDir -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
[void]$process.Handle

# From this point every outcome records post-run hashes: timeout, nonzero
# exit, report parse failure, and any exception.
$timedOut = $false
$stopError = $null
try {
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        $timedOut = $true
        $stopError = Stop-LaunchedProcess -Process $process
    }
} finally {
    $hashAfter = Write-HashSnapshot -DrawingPath $drawingPath -ModelPath $modelPath -RunDir $runDir -Phase 'after'
}

if ($timedOut) {
    $stopSuffix = '并等待其退出'
    if ($stopError) { $stopSuffix = '，但终止时出现问题: ' + $stopError }
    throw ('NX 清点超时，已终止本脚本启动的进程' + $stopSuffix + '；源 PRT 哈希已复核。')
}

$hashErrors = @($hashBefore.Values + $hashAfter.Values) | Where-Object { $_ -like '<hash-capture-error*' }
if ($hashErrors) { throw '源 PRT 哈希采集失败，已停止。' }
if ($hashBefore.drawing -ne $hashAfter.drawing -or $hashBefore.model -ne $hashAfter.model) {
    throw '源 PRT 哈希发生变化，属于硬失败。'
}

if ($process.ExitCode -ne 0) {
    throw ('NX 清点进程退出码非零: ' + $process.ExitCode + '；源文件哈希已复核。')
}

$reportIssue = Get-InventoryReportIssue -ReportPath $reportPath
if ($reportIssue) {
    throw ('清点报告无效: ' + $reportIssue + '；源文件哈希已复核。')
}

Write-Host 'GATE 2 INVENTORY: PASS (source hashes match; report structurally valid; raw evidence retained only under ignored artifacts)'
