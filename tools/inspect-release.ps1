# Inspects the portable release package and fails on forbidden content:
# Siemens DLLs, PRT/PDF samples, source files, logs, temp files, PDBs,
# secrets, or absolute developer paths. Emits a SHA-256 manifest written as
# UTF-8 (no BOM) and immediately verifies every package file against it.
#
# CR-07: the path audit scans EVERY payload as raw bytes (both ASCII and
# UTF-16LE forms), not just .txt/.md files, so paths embedded in binaries
# or symbol metadata are caught.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\inspect-release.ps1
#   powershell -NoProfile -File tools\inspect-release.ps1 -PackageRoot <dir> -ManifestPath <file>
#   powershell -NoProfile -File tools\inspect-release.ps1 -VerifyManifestOnly
param(
    [string]$PackageRoot,
    [string]$ManifestPath,
    [switch]$VerifyManifestOnly
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $PackageRoot) { $PackageRoot = Join-Path $repoRoot 'artifacts\release\package' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $repoRoot 'artifacts\release\manifest.sha256' }
if (-not (Test-Path $PackageRoot)) { throw ('未找到发布包目录: ' + $PackageRoot) }

function Get-PackageRelativePath {
    param([System.IO.FileInfo]$File)
    return $File.FullName.Substring($PackageRoot.Length + 1)
}

function Get-ManifestProblems {
    # 清单校验：逐行解析 "哈希␣␣相对路径"，要求与包内容一一对应且哈希一致。
    param([string]$ManifestFile)

    $found = @()
    if (-not (Test-Path -LiteralPath $ManifestFile)) {
        return @(' 清单文件不存在: ' + $ManifestFile)
    }

    $lines = [System.IO.File]::ReadAllLines($ManifestFile)
    $covered = @{}
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line -split '  ', 2
        if ($parts.Count -ne 2) {
            $found += (' 清单行格式错误: ' + $line)
            continue
        }

        $hash = $parts[0]
        $relative = $parts[1]
        if ($covered.ContainsKey($relative)) {
            $found += (' 清单重复条目: ' + $relative)
            continue
        }

        $covered[$relative] = $true
        $full = Join-Path $PackageRoot $relative
        if (-not (Test-Path -LiteralPath $full)) {
            $found += (' 清单引用了包内不存在的文件: ' + $relative)
            continue
        }

        $actual = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
        if ($actual -ne $hash) {
            $found += (' 清单哈希不一致: ' + $relative)
        }
    }

    foreach ($file in $files) {
        $relative = Get-PackageRelativePath -File $file
        if (-not $covered.ContainsKey($relative)) {
            $found += (' 文件未列入清单: ' + $relative)
        }
    }

    return $found
}

$files = @(Get-ChildItem $PackageRoot -Recurse -File)

if ($VerifyManifestOnly) {
    $verifyProblems = @(Get-ManifestProblems -ManifestFile $ManifestPath)
    if ($verifyProblems.Count -gt 0) {
        Write-Host '=== 清单复验失败 ==='
        foreach ($problem in $verifyProblems) { Write-Host $problem }
        throw ('清单复验失败，共 ' + $verifyProblems.Count + ' 个问题。')
    }

    Write-Host ('MANIFEST VERIFY OK: ' + $files.Count + ' 个文件的 SHA-256 与清单一致。')
    return
}

$problems = @()

foreach ($file in $files) {
    $name = $file.Name.ToLowerInvariant()
    if ($name -like 'nxopen*.dll') { $problems += (' Siemens DLL: ' + $file.FullName.Substring($PackageRoot.Length)) }
    if ($name.EndsWith('.prt')) { $problems += (' PRT 文件: ' + $file.FullName.Substring($PackageRoot.Length)) }
    if ($name.EndsWith('.pdf')) { $problems += (' PDF 文件: ' + $file.FullName.Substring($PackageRoot.Length)) }
    if ($name.EndsWith('.cs') -or $name.EndsWith('.csproj') -or $name.EndsWith('.slnx')) { $problems += (' 源文件: ' + $file.FullName.Substring($PackageRoot.Length)) }
    if ($name.EndsWith('.log') -or $name.EndsWith('.tmp') -or $name.EndsWith('.pdb') -or $name.EndsWith('.cache')) { $problems += (' 日志/临时/PDB: ' + $file.FullName.Substring($PackageRoot.Length)) }
    if ($name.EndsWith('.pfx') -or $name.EndsWith('.snk') -or $name.EndsWith('.key')) { $problems += (' 疑似密钥: ' + $file.FullName.Substring($PackageRoot.Length)) }
    if ($name -eq 'sheet-name-map.json' -or $name -eq 'export-name-map.json') { $problems += (' 私有名称映射: ' + $file.FullName.Substring($PackageRoot.Length)) }
}

# Developer-path audit across ALL payload bytes, in both ASCII and UTF-16LE
# forms, so embedded PDB/CodeView data or string literals cannot hide a
# developer checkout or NX installation path.
#
# Path policy (reviewed, see docs/verification/release/README.md):
#   * Forbidden outright: the developer checkout (E:\Codex), the offline
#     toolchain project (nx-step-launcher), the historical fixed NX root,
#     and user-profile drive paths. NX installation roots have no exemption.
#   * Generic build/source paths (containing \src\, \obj\, .cs/.cpp/.pdb…)
#     must match a REVIEWED upstream prefix: Microsoft's official .NET
#     runtime CI root (D:\a\_work\, inherent to the official runtime pack)
#     and PDFsharp's upstream dev root (D:\repos\empira\, inherent to the
#     official NuGet binary). Any other such path fails the gate and forces
#     a fresh review.
#   * Binary noise that merely resembles "X:\..." without source/build
#     indicators is not treated as a path.
$approvedPathPrefixes = @(
    'D:\a\_work\',
    'D:\repos\empira\'
)
$forbiddenLiterals = @('E:\Codex', 'D:\Program Files\Siemens\NX 10.0', 'nx-step-launcher')
$sourcePathCandidate = [regex]'[A-Za-z]:\\[^\x00-\x08\x0b\x0c\x0e-\x1f"<>|*?]{6,140}'
$sourcePathIndicator = [regex]'\\(src|source|obj)\\|\.(cs|cpp|hpp|pdb|vb|rs)([^A-Za-z0-9_]|$)'
foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $forms = @(
        [System.Text.Encoding]::GetEncoding(28591).GetString($bytes),
        [System.Text.Encoding]::Unicode.GetString($bytes)
    )
    foreach ($form in $forms) {
        $scanned = $form
        foreach ($approved in $approvedPathPrefixes) {
            $scanned = $scanned.Replace($approved, '')
        }

        $hit = $null
        foreach ($literal in $forbiddenLiterals) {
            if ($scanned.IndexOf($literal, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $hit = $literal
                break
            }
        }

        if ($null -eq $hit -and $scanned -match '[A-Za-z]:\\Users\\') {
            $hit = '用户目录绝对路径'
        }

        if ($null -eq $hit) {
            foreach ($candidateMatch in $sourcePathCandidate.Matches($scanned)) {
                $candidate = $candidateMatch.Value
                if (-not $sourcePathIndicator.IsMatch($candidate)) { continue }
                $allowed = $false
                foreach ($prefix in $approvedPathPrefixes) {
                    if ($candidate.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
                        $allowed = $true
                        break
                    }
                }

                if (-not $allowed) {
                    $hit = '未审知的绝对源码/构建路径: ' + $candidate
                    break
                }
            }
        }

        if ($null -ne $hit) {
            $problems += (' 开发机路径泄漏: ' + (Get-PackageRelativePath -File $file) + '（' + $hit + '）')
            break
        }
    }
}

# Manifest: UTF-8 without BOM so non-ASCII package filenames round-trip.
$manifestLines = @(foreach ($file in ($files | Sort-Object FullName)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash, (Get-PackageRelativePath -File $file)
})
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($ManifestPath, [string[]]$manifestLines, $utf8NoBom)

Write-Host ('文件总数: ' + $files.Count)
$files | ForEach-Object { Write-Host ('  ' + (Get-PackageRelativePath -File $_)) }
Write-Host ('SHA-256 清单: ' + $ManifestPath)

$manifestProblems = @(Get-ManifestProblems -ManifestFile $ManifestPath)
$problems += $manifestProblems

if ($problems.Count -gt 0) {
    Write-Host '=== 违禁内容 ==='
    foreach ($problem in $problems) { Write-Host $problem }
    throw ('发布包检查失败，共 ' + $problems.Count + ' 个问题。')
}

Write-Host 'INSPECT OK: 无 Siemens DLL、无私有样例、无源码/日志/PDB/密钥、无开发机路径；清单已生成并逐文件复验。'
