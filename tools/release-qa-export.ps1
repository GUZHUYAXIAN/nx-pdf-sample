# Release-QA controlled export: runs the packaged worker (from the clean
# extraction directory) through run_managed.exe on a disposable fixture,
# then validates the produced PDF via PdfGate. Live in artifacts only.
param(
    [Parameter(Mandatory = $true)][string]$CleanDir,
    [Parameter(Mandatory = $true)][string]$FixtureDrawing,
    [Parameter(Mandatory = $true)][string]$QaDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runManaged = 'D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe'
$env:DOTNET_ROOT = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet'
$env:DOTNET_MULTILEVEL_LOOKUP = '0'

$workerExe = Join-Path $CleanDir 'worker\NxDrawingPdfExporter.Worker.exe'
if (-not (Test-Path $workerExe)) { throw '干净解压目录缺少 Worker。' }

$tempPdf = Join-Path $QaDir 'release-exported.pdf'
$job = [ordered]@{
    ProtocolVersion      = '1'
    RunId                = 'release-qa'
    Items                = @([ordered]@{
        SourcePath          = $FixtureDrawing
        FinalOutputPath     = (Join-Path $QaDir 'release-final.pdf')
        WorkerTempOutputPath = $tempPdf
    })
    OutputMode           = 0
    ExistingPdfPolicy    = 1
    ResultPath           = (Join-Path $QaDir 'result.json')
    CancellationFlagPath = (Join-Path $QaDir 'cancel.flag')
}
$jobPath = Join-Path $QaDir 'job.json'
$job | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $jobPath -Encoding UTF8

$process = Start-Process -FilePath $runManaged -ArgumentList @('"' + $workerExe + '"', '--run-job', '"' + $jobPath + '"') -WorkingDirectory $QaDir -RedirectStandardOutput (Join-Path $QaDir 'stdout.txt') -RedirectStandardError (Join-Path $QaDir 'stderr.txt') -PassThru -Wait
Write-Host ('EXIT (non-authoritative): ' + $process.ExitCode)

$gateExe = Join-Path $repoRoot 'tools\NxDrawingPdfExporter.PdfGate\bin\Debug\net10.0-windows\NxDrawingPdfExporter.PdfGate.exe'
& $gateExe inspect $tempPdf (Join-Path $QaDir 'pdf-inspection.json')
if ($LASTEXITCODE -ne 0) { throw 'PDF 校验失败。' }
Get-Content (Join-Path $QaDir 'pdf-inspection.json') -Raw -Encoding UTF8

$result = Get-Content (Join-Path $QaDir 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host ('RESULT Success Status[0]: ' + $result.Files[0].Status + ' (期望 0 = Success)')
if ([int]$result.Files[0].Status -ne 0) { throw '发布 Worker 导出失败。' }
$inspection = Get-Content (Join-Path $QaDir 'pdf-inspection.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $inspection.HasPdfHeader -or $inspection.PageCount -ne 1) { throw 'PDF 校验不符。' }
Write-Host 'RELEASE EXPORT QA: PASS'
