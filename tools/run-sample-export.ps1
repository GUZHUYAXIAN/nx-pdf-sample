# Gate 2 sample export harness. Protects the private source pair on every
# attempt (SHA-256 before/after around all outcomes), launches only its own
# run_managed.exe process, and validates the produced PDF through the
# production PdfSharpInspector. Raw evidence stays in gitignored artifacts.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\run-sample-export.ps1 -DrawingPrt <prt> -AssociatedModelPrt <prt>
param(
    [string]$DrawingPrt = '',
    [string]$AssociatedModelPrt = '',
    [string]$Configuration = 'Debug',
    [int]$TimeoutSeconds = 900,
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

# Structural validation of the export report. Returns $null when coherent.
function Get-ExportReportIssue {
    param([string]$ReportPath)
    if (-not (Test-Path -LiteralPath $ReportPath)) { return '导出报告文件不存在。' }
    $file = Get-Item -LiteralPath $ReportPath
    if ($file.Length -le 0) { return '导出报告为空文件。' }

    try {
        $report = Get-Content -LiteralPath $ReportPath -Raw -Encoding UTF8 | ConvertFrom-Json
    } catch {
        return '导出报告不是可解析的 JSON。'
    }

    if ($null -eq $report) { return '导出报告 JSON 为空。' }
    if ($report.Success -ne $true) { return ('导出报告未报告成功' + $(if ($report.Failure) { ': ' + $report.Failure } else { '。' })) }
    if (-not [string]::IsNullOrWhiteSpace($report.Failure)) { return '成功报告携带失败原因。' }
    if ($report.SourceUnchanged -ne $true) { return '成功报告未确认源 PRT 未变化。' }
    if ($null -eq $report.LoadDiagnostics -or $report.LoadDiagnostics -isnot [Array]) { return '缺少加载诊断数组。' }
    if ($null -eq $report.Sheets -or $report.Sheets -isnot [Array]) { return '缺少图纸页数组。' }
    if ($report.Sheets.Count -lt 1) { return '成功报告未包含任何图纸页事实。' }

    $seenTokens = @{}
    $selectedCount = 0
    for ($index = 0; $index -lt $report.Sheets.Count; $index++) {
        $sheet = $report.Sheets[$index]
        if ($null -eq $sheet) { return ('图纸页 ' + $index + ' 为空。') }
        if ($sheet.NativeIndex -ne $index -or $sheet.ExportOrderIndex -ne $index) { return ('图纸页 ' + $index + ' 的导出顺序索引与数组位置不一致。') }
        if ($null -eq $sheet.DraftingViewCount -or $sheet.DraftingViewCount -lt 0) { return ('图纸页 ' + $index + ' 制图视图数量无效。') }
        if (-not [string]::IsNullOrEmpty($sheet.NameToken) -and $sheet.NameToken -cnotmatch '^[0-9a-f]{16}$') { return ('图纸页 ' + $index + ' 名称令牌无效。') }
        if ($seenTokens.ContainsKey($sheet.NameToken)) { return ('图纸页 ' + $index + ' 名称令牌重复。') }
        $seenTokens[$sheet.NameToken] = $true
        if ($sheet.Selected) {
            $selectedCount++
            if ($sheet.DraftingViewCount -lt 1) { return ('选中页 ' + $index + ' 缺少实际制图视图。') }
            if (-not (Test-PositiveNumber $sheet.Length) -or -not (Test-PositiveNumber $sheet.Height)) { return ('选中页 ' + $index + ' 图幅尺寸无效。') }
        } elseif ($sheet.DraftingViewCount -ne 0) {
            return ('未选中页 ' + $index + ' 包含实际制图视图。')
        }
    }

    if ($selectedCount -ne $report.SelectedCount) { return '选中页数量与 SelectedCount 不一致。' }
    if ($report.Sheets.Count - $selectedCount -ne $report.SkippedCount) { return '跳过页数量与 SkippedCount 不一致。' }
    return $null
}

# Converts NX sheet size to PDF points. PDF points are 1/72 inch.
function ConvertTo-Points {
    param([double]$Value, [string]$Units)
    if ($Units -eq 'Inches') { return $Value * 72.0 }
    return $Value * 72.0 / 25.4
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
        throw '缺少必要本地文件。'
    }
}

& $taskDotnet build (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\NxDrawingPdfExporter.Worker.csproj') -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Worker 构建失败。' }
& $taskDotnet build (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.PdfGate\NxDrawingPdfExporter.PdfGate.csproj') -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'PdfGate 构建失败。' }

$workerExe = Get-ChildItem (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\bin') -Recurse -Filter 'NxDrawingPdfExporter.Worker.exe' |
    Where-Object { $_.FullName -like "*\$Configuration\*" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$pdfGateExe = Get-ChildItem (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.PdfGate\bin') -Recurse -Filter 'NxDrawingPdfExporter.PdfGate.exe' |
    Where-Object { $_.FullName -like "*\$Configuration\*" } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($workerExe) -or [string]::IsNullOrWhiteSpace($pdfGateExe)) { throw '未找到可执行文件。' }

$runDir = Join-Path $repoRoot ('artifacts\gate-2\export-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$hashBefore = Write-HashSnapshot -DrawingPath $drawingPath -ModelPath $modelPath -RunDir $runDir -Phase 'before'

$tempPdfPath = Join-Path $runDir 'exported.pdf'
$reportPath = Join-Path $runDir 'export-report.json'
$stdoutPath = Join-Path $runDir 'stdout.txt'
$stderrPath = Join-Path $runDir 'stderr.txt'
$quotedWorker = '"' + $workerExe + '"'
$quotedDrawing = '"' + $drawingPath + '"'
$quotedTempPdf = '"' + $tempPdfPath + '"'
$quotedReport = '"' + $reportPath + '"'
$process = Start-Process -FilePath $runManaged -ArgumentList @($quotedWorker, '--export', $quotedDrawing, $quotedTempPdf, $quotedReport) -WorkingDirectory $runDir -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
[void]$process.Handle

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
    throw ('NX 导出超时，已终止本脚本启动的进程' + $stopSuffix + '；源 PRT 哈希已复核。')
}

$hashErrors = @($hashBefore.Values + $hashAfter.Values) | Where-Object { $_ -like '<hash-capture-error*' }
if ($hashErrors) { throw '源 PRT 哈希采集失败，已停止。' }
if ($hashBefore.drawing -ne $hashAfter.drawing -or $hashBefore.model -ne $hashAfter.model) {
    throw '源 PRT 哈希发生变化，属于硬失败。'
}

if ($process.ExitCode -ne 0) {
    throw ('NX 导出进程退出码非零: ' + $process.ExitCode + '；源文件哈希已复核。')
}

$reportIssue = Get-ExportReportIssue -ReportPath $reportPath
if ($reportIssue) {
    throw ('导出报告无效: ' + $reportIssue + '；源文件哈希已复核。')
}

$report = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($report.SelectedCount -lt 1) {
    throw ('导出报告未包含任何选中页；源文件哈希已复核。')
}
if (-not (Test-Path -LiteralPath $tempPdfPath)) {
    throw ('报告成功但临时 PDF 不存在；源文件哈希已复核。')
}

# Managed PDF inspection through the production PdfSharpInspector.
# The gate host runs on the repo-local .NET 10 SDK runtime; these are
# process-local environment variables, not machine changes.
$env:DOTNET_ROOT = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$inspectionPath = Join-Path $runDir 'pdf-inspection.json'
& $pdfGateExe inspect $tempPdfPath $inspectionPath
if ($LASTEXITCODE -ne 0) { throw ('PDF 检查工具失败，详见 ' + $inspectionPath) }
$inspection = Get-Content -LiteralPath $inspectionPath -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $inspection.HasPdfHeader) { throw 'PDF 缺少有效文件头。' }
if ($inspection.PageCount -ne $report.SelectedCount) {
    throw ('PDF 页数 ' + $inspection.PageCount + ' 与选中页数 ' + $report.SelectedCount + ' 不一致。')
}

# Every page MediaBox must match the NX sheet size of the same export order.
$selectedSheets = @($report.Sheets | Where-Object { $_.Selected })
for ($pageIndex = 0; $pageIndex -lt $inspection.PageCount; $pageIndex++) {
    $sheet = $selectedSheets[$pageIndex]
    $page = $inspection.PageSizes[$pageIndex]
    $expectedWidth = ConvertTo-Points -Value ([double]$sheet.Length) -Units $sheet.Units
    $expectedHeight = ConvertTo-Points -Value ([double]$sheet.Height) -Units $sheet.Units
    $tolerance = 1.5
    if ([math]::Abs($page.widthPoints - $expectedWidth) -gt $tolerance -or [math]::Abs($page.heightPoints - $expectedHeight) -gt $tolerance) {
        throw ('第 ' + ($pageIndex + 1) + ' 页尺寸 ' + $page.widthPoints + 'x' + $page.heightPoints + ' 与图纸尺寸 ' + $expectedWidth + 'x' + $expectedHeight + ' 不一致。')
    }
}

Write-Host ('GATE 2 EXPORT: PASS (source hashes match; report valid; PDF pages=' + $inspection.PageCount + '; sizes match selected sheets in export order; evidence retained only under ignored artifacts)')
Write-Host ('RUN DIR: ' + $runDir)
