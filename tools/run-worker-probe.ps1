<#
.SYNOPSIS
    Gate 1 harness: launches NxDrawingPdfExporter.Probe through the verified local
    Siemens NX 10 run_managed.exe and records launch evidence.

.DESCRIPTION
    - Builds the probe (offline, repo-local NuGet cache).
    - Creates a unique gitignored run directory under artifacts\gate-1\.
    - Invokes run_managed.exe <probe.exe> <report-path> with a watchdog timeout.
    - Keeps raw stdout/stderr in the gitignored run directory.
    - Writes privacy-scrubbed copies (report JSON + machine-readable summary)
      into docs\verification\gate-1\ for commit.

.NOTES
    Exit code of this script mirrors gate success: 0 = both sessions proven.
    Usernames are scrubbed from committed evidence. Sample PRTs are never touched.
#>

param(
    [string]$Configuration = 'Debug',
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$runManaged = 'D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe'

if (-not (Test-Path -LiteralPath $runManaged)) {
    throw "未找到已验证的 NX 10 启动器：$runManaged"
}

# Repo-local tool caches (process-level env only; nothing global is modified).
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = Join-Path $repoRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repoRoot '.tools\nuget-http-cache'

# --- Build probe -------------------------------------------------------------
$probeCsproj = Join-Path $repoRoot 'tools\NxDrawingPdfExporter.Probe\NxDrawingPdfExporter.Probe.csproj'
& $taskDotnet build $probeCsproj -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Probe 构建失败（exit $LASTEXITCODE）。"
}

$probeExe = Get-ChildItem (Join-Path $repoRoot 'tools\NxDrawingPdfExporter.Probe\bin') `
    -Recurse -Filter 'NxDrawingPdfExporter.Probe.exe' -ErrorAction Stop |
    Where-Object { $_.FullName -like "*\$Configuration\*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $probeExe) {
    throw '未找到构建产物 NxDrawingPdfExporter.Probe.exe。'
}

# --- Run directory -----------------------------------------------------------
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$runDir = Join-Path $repoRoot "artifacts\gate-1\$stamp"
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

$reportPath = Join-Path $runDir 'probe-report.json'
$stdoutPath = Join-Path $runDir 'stdout.txt'
$stderrPath = Join-Path $runDir 'stderr.txt'

# --- Launch with watchdog ----------------------------------------------------
$quotedProbe = '"' + $probeExe + '"'
$quotedReport = '"' + $reportPath + '"'

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$process = Start-Process -FilePath $runManaged `
    -ArgumentList @($quotedProbe, $quotedReport) `
    -WorkingDirectory $runDir `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

# Acquire the process handle BEFORE it can exit; otherwise .ExitCode reads as null on PS 5.1.
[void]$process.Handle

$timedOut = $false
while (-not $process.HasExited) {
    if ($sw.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
        $timedOut = $true
        $process.Kill()
        break
    }

    Start-Sleep -Milliseconds 2000
}

if (-not $timedOut) {
    $process.WaitForExit()
}

$sw.Stop()

$exitCode = if ($timedOut) { $null } else { $process.ExitCode }

# --- Collect raw facts -------------------------------------------------------
$reportJson = $null
if (Test-Path -LiteralPath $reportPath) {
    $reportJson = Get-Content -LiteralPath $reportPath -Raw -Encoding UTF8
}

$summary = [ordered]@{
    utcTimestamp      = (Get-Date).ToUniversalTime().ToString('o')
    launcher          = $runManaged
    launcherFileVersion = (Get-Item -LiteralPath $runManaged).VersionInfo.FileVersion
    commandLine       = ('"' + $runManaged + '" "' + $probeExe + '" "' + $reportPath + '"')
    probeExe          = $probeExe
    probeFileVersion  = (Get-Item -LiteralPath $probeExe).VersionInfo.FileVersion
    workingDirectory  = $runDir
    timeoutSeconds    = $TimeoutSeconds
    timedOut          = $timedOut
    exitCode          = $exitCode
    elapsedSeconds    = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    reportWritten     = [bool]$reportJson
    report            = if ($reportJson) { $reportJson.Trim() | ConvertFrom-Json } else { $null }
}

function Hide-User([string]$text) {
    $user = [System.Environment]::UserName
    return $text.Replace('\Users\' + $user, '\Users\<user>').Replace($user, '<user>')
}

# --- Committed evidence (privacy-scrubbed) -----------------------------------
$evidenceDir = Join-Path $repoRoot 'docs\verification\gate-1'
New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null

$summaryJson = Hide-User (($summary | ConvertTo-Json -Depth 4))
Set-Content -LiteralPath (Join-Path $evidenceDir 'last-run-summary.json') -Value $summaryJson -Encoding UTF8

if ($reportJson) {
    Set-Content -LiteralPath (Join-Path $evidenceDir 'last-probe-report.json') `
        -Value (Hide-User $reportJson) -Encoding UTF8
}

# --- Console verdict ---------------------------------------------------------
Write-Host '===== GATE 1 SUMMARY ====='
Write-Host ($summaryJson | Hide-User)
Write-Host ('exitCode={0} session={1} ufSession={2} timedOut={3}' -f `
    $exitCode,
    $summary.report.nxSessionAvailable,
    $summary.report.ufSessionAvailable,
    $timedOut)

$gatePassed = (-not $timedOut) -and ($exitCode -eq 0) -and `
    ($summary.report -ne $null) -and `
    ($summary.report.nxSessionAvailable -eq $true) -and `
    ($summary.report.ufSessionAvailable -eq $true) -and `
    ($summary.report.processBitness -eq 'x64')

if ($gatePassed) {
    Write-Host 'GATE 1: PASS'
    exit 0
}

Write-Host 'GATE 1: FAIL (hard stop per plan Task 3 Step 5)'
exit 1
