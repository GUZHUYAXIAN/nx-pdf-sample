param([Parameter(Mandatory=$true)][string]$ManagedDirectory, [Parameter(Mandatory=$true)][string]$Generation)
$ErrorActionPreference = 'Stop'
$expected = if ($Generation -eq '2506') { @('2506.0.0.0','2506.1700.0.0','2506.0.0.0') }
    elseif ($Generation -eq '10') { @('10.0.0.24','10.0.0.23','4.0.0.0') }
    else { throw 'Unsupported NX generation.' }
$names = @('NXOpen.dll','NXOpen.UF.dll','NXOpen.Utilities.dll')
for ($index = 0; $index -lt $names.Count; $index++) {
    $path = Join-Path $ManagedDirectory $names[$index]
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing local NX component: $($names[$index])" }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion -cne $expected[$index]) {
        throw "Unverified local NX component version: $($names[$index]); expected $($expected[$index])"
    }
}
