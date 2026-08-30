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
