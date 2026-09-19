# Static policy test for the offline release entrypoint.
param()

$ErrorActionPreference = 'Stop'
$publishScript = Join-Path $PSScriptRoot 'publish-portable.ps1'
$lines = @(Get-Content -LiteralPath $publishScript)
$publishCommands = @($lines | Where-Object { $_ -match '^& \$taskDotnet publish ' })

if ($publishCommands.Count -ne 1) {
    throw ('Expected exactly one dotnet publish command, found ' + $publishCommands.Count + '.')
}

if ($publishCommands[0] -notmatch '(?:^|\s)--no-restore(?:\s|$)') {
    throw 'Offline policy violation: dotnet publish must use --no-restore.'
}

Write-Host 'PUBLISH PORTABLE POLICY TEST: PASS'

$text = Get-Content -LiteralPath $publishScript -Raw
foreach ($required in @('worker\nx10', 'worker\nx2506', 'NxDrawingPdfExporter.Worker.Nx2506.exe')) {
    if (-not $text.Contains($required)) { throw "Dual Worker package target missing: $required" }
}
if ($text -match 'Sort-Object LastWriteTimeUtc') { throw 'Worker selection must not depend on stale output timestamps.' }
Write-Host 'DUAL WORKER PACKAGE POLICY: PASS'