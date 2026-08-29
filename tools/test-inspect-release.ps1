# Self-test for tools\inspect-release.ps1 using disposable fixture packages
# under the system temp directory. Covers the CR-07/CR-08 regressions:
#   1. A binary payload embedding a developer checkout path fails inspection.
#   2. A clean fixture with Chinese/space filenames produces a UTF-8 manifest
#      whose entries round-trip exactly and verify byte-for-byte.
#   3. Mutating one byte after generation makes manifest verification fail.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\test-inspect-release.ps1
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$inspector = Join-Path $PSScriptRoot 'inspect-release.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('nxpdf-inspect-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null

function Invoke-Inspector {
    param([string]$Package, [string]$Manifest, [switch]$VerifyOnly)
    if ($VerifyOnly) {
        & powershell -NoProfile -File $inspector -PackageRoot $Package -ManifestPath $Manifest -VerifyManifestOnly | Out-Null
    }
    else {
        & powershell -NoProfile -File $inspector -PackageRoot $Package -ManifestPath $Manifest | Out-Null
    }

    return $LASTEXITCODE
}

try {
    Write-Host '=== 自测 1：含开发机路径的二进制载荷必须被拒绝 ==='
    $dirtyPackage = Join-Path $fixtureRoot 'dirty'
    New-Item -ItemType Directory -Path $dirtyPackage | Out-Null
    $leaked = 'E:\Codex\projects\nx-pdf-sample\src\NxDrawingPdfExporter.Worker\obj\Fake.pdb'
    [System.IO.File]::WriteAllBytes((Join-Path $dirtyPackage 'fake.exe'), [System.Text.Encoding]::ASCII.GetBytes($leaked))
    [System.IO.File]::WriteAllBytes((Join-Path $dirtyPackage 'utf16.dll'), [System.Text.Encoding]::Unicode.GetBytes('D:\Program Files\Siemens\NX 10.0\ugii'))
    $dirtyExit = Invoke-Inspector -Package $dirtyPackage -Manifest (Join-Path $fixtureRoot 'dirty.sha256')
    if ($dirtyExit -eq 0) { throw 'CR-07 自测失败：含开发者路径的载荷未被拒绝。' }
    Write-Host 'PASS：ASCII 与 UTF-16LE 载荷均被拒绝。'

    Write-Host '=== 自测 2：干净包 + 中文/空格文件名的 UTF-8 清单回环 ==='
    $cleanPackage = Join-Path $fixtureRoot 'clean'
    New-Item -ItemType Directory -Path $cleanPackage | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $cleanPackage 'NX图纸批量导出工具.exe'), [System.Text.Encoding]::ASCII.GetBytes('clean executable bytes'))
    [System.IO.File]::WriteAllBytes((Join-Path $cleanPackage '说 明.txt'), [System.Text.Encoding]::UTF8.GetBytes('便携版说明'))
    New-Item -ItemType Directory -Path (Join-Path $cleanPackage 'worker') | Out-Null
    [System.IO.File]::WriteAllBytes((Join-Path $cleanPackage 'worker\NxDrawingPdfExporter.Worker.exe'), [System.Text.Encoding]::ASCII.GetBytes('clean worker bytes'))
    $cleanManifest = Join-Path $fixtureRoot 'clean.sha256'
    $cleanExit = Invoke-Inspector -Package $cleanPackage -Manifest $cleanManifest
    if ($cleanExit -ne 0) { throw 'CR-08 自测失败：干净包未通过检查。' }

    $manifestLines = [System.IO.File]::ReadAllLines($cleanManifest)
    if ($manifestLines.Count -ne 3) { throw ('清单条目数不符: ' + $manifestLines.Count) }
    $joined = $manifestLines -join "`n"
    if (-not $joined.Contains('NX图纸批量导出工具.exe')) { throw '清单丢失中文文件名。' }
    if (-not $joined.Contains('说 明.txt')) { throw '清单丢失中文+空格文件名。' }
    foreach ($line in $manifestLines) {
        $parts = $line -split '  ', 2
        if ($parts.Count -ne 2) { throw '清单行格式错误。' }
        $full = Join-Path $cleanPackage $parts[1]
        if (-not (Test-Path -LiteralPath $full)) { throw ('清单路径回环失败: ' + $parts[1]) }
        $actual = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
        if ($actual -ne $parts[0]) { throw ('清单哈希不符: ' + $parts[1]) }
    }
    Write-Host 'PASS：中文/空格文件名逐字节回环，哈希一致。'

    Write-Host '=== 自测 3：清单复验通过，篡改一字节后必须失败 ==='
    $verifyExit = Invoke-Inspector -Package $cleanPackage -Manifest $cleanManifest -VerifyOnly
    if ($verifyExit -ne 0) { throw '清单复验对未改动的包报告失败。' }

    $target = Join-Path $cleanPackage 'worker\NxDrawingPdfExporter.Worker.exe'
    $bytes = [System.IO.File]::ReadAllBytes($target)
    $bytes[0] = [byte]($bytes[0] -bxor 0xFF)
    [System.IO.File]::WriteAllBytes($target, $bytes)
    $tamperExit = Invoke-Inspector -Package $cleanPackage -Manifest $cleanManifest -VerifyOnly
    if ($tamperExit -eq 0) { throw 'CR-08 自测失败：篡改后复验未失败。' }
    Write-Host 'PASS：篡改一字节即被清单复验发现。'

    Write-Host 'INSPECT-RELEASE SELF TEST: PASS'
}
finally {
    if (Test-Path $fixtureRoot) { Remove-Item $fixtureRoot -Recurse -Force }
}
