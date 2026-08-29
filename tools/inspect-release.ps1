# Inspects the portable release package and fails on forbidden content:
# Siemens DLLs, PRT/PDF samples, source files, logs, temp files, PDBs,
# secrets, or absolute developer paths. Emits a SHA-256 manifest.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\inspect-release.ps1
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $repoRoot 'artifacts\release\package'
if (-not (Test-Path $packageRoot)) { throw '未找到发布包目录 artifacts\release\package。' }

$problems = @()
$files = Get-ChildItem $packageRoot -Recurse -File

foreach ($file in $files) {
    $name = $file.Name.ToLowerInvariant()
    if ($name -like 'nxopen*.dll') { $problems += (' Siemens DLL: ' + $file.FullName.Substring($packageRoot.Length)) }
    if ($name.EndsWith('.prt')) { $problems += (' PRT 文件: ' + $file.FullName.Substring($packageRoot.Length)) }
    if ($name.EndsWith('.pdf')) { $problems += (' PDF 文件: ' + $file.FullName.Substring($packageRoot.Length)) }
    if ($name.EndsWith('.cs') -or $name.EndsWith('.csproj') -or $name.EndsWith('.slnx')) { $problems += (' 源文件: ' + $file.FullName.Substring($packageRoot.Length)) }
    if ($name.EndsWith('.log') -or $name.EndsWith('.tmp') -or $name.EndsWith('.pdb') -or $name.EndsWith('.cache')) { $problems += (' 日志/临时/PDB: ' + $file.FullName.Substring($packageRoot.Length)) }
    if ($name.EndsWith('.pfx') -or $name.EndsWith('.snk') -or $name.EndsWith('.key')) { $problems += (' 疑似密钥: ' + $file.FullName.Substring($packageRoot.Length)) }
    if ($name -eq 'sheet-name-map.json' -or $name -eq 'export-name-map.json') { $problems += (' 私有名称映射: ' + $file.FullName.Substring($packageRoot.Length)) }
}

# Developer-absolute-path audit in text payload files.
foreach ($file in $files | Where-Object { $_.Extension -in @('.txt', '.md') }) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8 -ErrorAction SilentlyContinue
    if ($null -eq $text) { continue }
    if ($text -match 'E:\\Codex' -or $text -match 'D:\\Program Files' -or $text -match 'nx-step-launcher') {
        $problems += (' 开发机绝对路径: ' + $file.FullName.Substring($packageRoot.Length))
    }
}

$manifestLines = foreach ($file in ($files | Sort-Object FullName)) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    '{0}  {1}' -f $hash, $file.FullName.Substring($packageRoot.Length + 1)
}
$manifestPath = Join-Path $repoRoot 'artifacts\release\manifest.sha256'
$manifestLines | Set-Content -LiteralPath $manifestPath -Encoding ASCII

Write-Host ('文件总数: ' + $files.Count)
$files | ForEach-Object { Write-Host ('  ' + $_.FullName.Substring($packageRoot.Length + 1)) }
Write-Host ('SHA-256 清单: ' + $manifestPath)

if ($problems.Count -gt 0) {
    Write-Host '=== 违禁内容 ==='
    foreach ($problem in $problems) { Write-Host $problem }
    throw ('发布包检查失败，共 ' + $problems.Count + ' 个问题。')
}

Write-Host 'INSPECT OK: 无 Siemens DLL、无私有样例、无源码/日志/PDB/密钥、无开发机绝对路径。'
