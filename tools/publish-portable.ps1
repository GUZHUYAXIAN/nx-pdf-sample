# Builds the portable release package:
#   package\
#     NX图纸批量导出工具.exe      (GUI, win-x64 self-contained single file)
#     worker\                     (net48 x64 worker files, no Siemens binaries)
#     THIRD-PARTY-NOTICES.txt
#     说明.txt
# Output lives under gitignored artifacts\release\. No network access: all
# packages resolve from the repo-local NuGet cache.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\publish-portable.ps1
param(
    [string]$PackageCache,
    [string]$ReleaseRoot,
    [string]$ReleaseVersion,
    [string]$DotnetPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$taskDotnet = if ($DotnetPath) { (Resolve-Path -LiteralPath $DotnetPath).Path } else { (Get-Command dotnet -ErrorAction Stop).Source }
if ($ReleaseVersion) {
    if ($ReleaseVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'ReleaseVersion must be a three-part numeric version.' }
    [xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw
    if ($props.SelectSingleNode('/Project/PropertyGroup/Version').InnerText -ne $ReleaseVersion) { throw 'ReleaseVersion differs from Directory.Build.props.' }
}
if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\dotnet-cli-home' }
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
if (-not $PackageCache) { $PackageCache = Join-Path $repoRoot '.nuget' }
if (-not (Test-Path -LiteralPath $PackageCache -PathType Container)) { throw 'Existing offline package cache is required.' }
$env:NUGET_PACKAGES = $PackageCache
$env:NUGET_HTTP_CACHE_PATH = Join-Path $repoRoot '.tools\nuget-http-cache'

Write-Host '=== Release 测试 ==='
& $taskDotnet test (Join-Path $repoRoot 'NxDrawingPdfExporter.slnx') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release 测试失败。' }

Write-Host '=== Release 构建 ==='
& $taskDotnet build (Join-Path $repoRoot 'NxDrawingPdfExporter.slnx') -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release 构建失败。' }

if (-not $ReleaseRoot) { $ReleaseRoot = Join-Path $repoRoot 'artifacts\release' }
$packageRoot = Join-Path $releaseRoot 'package'
if (Test-Path -LiteralPath $releaseRoot) { throw 'Release directory already exists; select a fresh -ReleaseRoot to preserve prior evidence.' }
New-Item -ItemType Directory -Path $packageRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $packageRoot 'worker') | Out-Null

Write-Host '=== 发布 GUI（win-x64 自包含单文件） ==='
& $taskDotnet publish (Join-Path $repoRoot 'src\NxDrawingPdfExporter.App\NxDrawingPdfExporter.App.csproj') -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:DebugType=None /p:DebugSymbols=false -o (Join-Path $releaseRoot 'gui-publish')
if ($LASTEXITCODE -ne 0) { throw 'GUI 发布失败。' }

$guiPublish = Join-Path $releaseRoot 'gui-publish'
$guiExe = Join-Path $guiPublish 'NxDrawingPdfExporter.App.exe'
if (-not (Test-Path $guiExe)) { throw '未找到 GUI 单文件发布输出。' }
Copy-Item $guiExe (Join-Path $packageRoot 'NX图纸批量导出工具.exe')

Write-Host '=== Copy two independently built Workers ==='
$workerTargets = @(
    @{ Project = 'NxDrawingPdfExporter.Worker'; Exe = 'NxDrawingPdfExporter.Worker.exe'; Destination = 'worker\nx10' },
    @{ Project = 'NxDrawingPdfExporter.Worker.Nx2506'; Exe = 'NxDrawingPdfExporter.Worker.Nx2506.exe'; Destination = 'worker\nx2506' }
)
foreach ($target in $workerTargets) {
    $workerDir = Join-Path $repoRoot ('src\' + $target.Project + '\bin\Release\net48\win-x64')
    $destination = Join-Path $packageRoot $target.Destination
    New-Item -ItemType Directory -Path $destination | Out-Null
    foreach ($name in @($target.Exe, ($target.Exe + '.config'), 'NxDrawingPdfExporter.Contracts.dll', 'NxDrawingPdfExporter.Core.dll')) {
        $source = Join-Path $workerDir $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing Worker payload: $name" }
        Copy-Item -LiteralPath $source -Destination $destination
    }
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination $packageRoot
Copy-Item (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt') (Join-Path $packageRoot 'THIRD-PARTY-NOTICES.txt')

@'
NX 图纸批量导出工具（便携版）

1. 双击 NX图纸批量导出工具.exe 运行；无需安装任何运行库。
2. 支持 Windows 11 x64、NX 10.0.0.24 与已验证 NX 2506 组件组合（NXOpen 2506.0.0.0 / UF 2506.1700.0.0 / Utilities 2506.0.0.0）。双版本无有效保存项时必须明确选择；可手动选择根目录。
3. 选择扫描文件夹或手动多选 PRT，选择输出方式后执行预检查，再开始导出。
4. 导出过程逐个处理文件；导出的 PDF 会先通过校验再正式发布。
5. 本工具不联网、不修改源 PRT、不修改 NX 安装和系统配置。
6. v2.0.1 双版本更新。维护者已手动试用并确认问题修复；跨 NX 版本打开旧图纸仍应核对尺寸标注，不保证所有 NX 2506 补丁及制图特性。
7. 100%/125%/150% DPI 完整矩阵、物理断网及其他电脑验收未逐项完成。不得混用旧包 Worker。
'@ | Set-Content -LiteralPath (Join-Path $packageRoot '说明.txt') -Encoding UTF8

# Record tracked source identity; unrelated local probes and evidence stay private.
Push-Location $repoRoot
try {
    $head = (& git rev-parse HEAD).Trim()
    $sourcePaths = @(& git ls-files --cached -- src tests build tools Directory.Build.props Directory.Packages.props global.json NxDrawingPdfExporter.slnx)
    $trackedChanges = @(& git status --porcelain --untracked-files=no)
    if ($ReleaseVersion -and $trackedChanges.Count) { throw 'Versioned releases require a clean tracked working tree.' }
    $sourceLines = @($sourcePaths | Sort-Object -Unique | ForEach-Object {
        '{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $repoRoot $_) -Algorithm SHA256).Hash, $_
    })
    [IO.File]::WriteAllLines((Join-Path $releaseRoot 'source-manifest.sha256'), $sourceLines, (New-Object Text.UTF8Encoding($false)))
    $sourceIdentity = (Get-FileHash -LiteralPath (Join-Path $releaseRoot 'source-manifest.sha256')).Hash
    $identity = if ($ReleaseVersion) { "NX Drawing PDF Exporter v$ReleaseVersion" } else { 'Local dual-NX candidate' }
    @($identity, "Source commit: $head", "Tracked changes: $($trackedChanges.Count)", "Source manifest SHA256: $sourceIdentity", "Maintainer reports successful manual use; full DPI/offline/other-machine matrix remains unverified.") |
        Set-Content -LiteralPath (Join-Path $packageRoot 'build-info.txt') -Encoding UTF8
    & git status --short | Set-Content -LiteralPath (Join-Path $releaseRoot 'working-tree-status.txt') -Encoding UTF8
} finally { Pop-Location }
& (Join-Path $PSScriptRoot 'inspect-release.ps1') -PackageRoot $packageRoot -ManifestPath (Join-Path $releaseRoot 'manifest.sha256')
$zipName = if ($ReleaseVersion) { "NX-Drawing-PDF-Exporter-v$ReleaseVersion-win-x64.zip" } else { 'NX-Drawing-PDF-Exporter-dual-NX-candidate-win-x64.zip' }
$zip = Join-Path $releaseRoot $zipName
Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $packageRoot).FullName -DestinationPath $zip
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash + '  ' + [IO.Path]::GetFileName($zip) |
    Set-Content -LiteralPath (Join-Path $releaseRoot 'candidate.sha256') -Encoding ASCII
Write-Host 'PUBLISH OK'
Write-Host ('PACKAGE ROOT: ' + $packageRoot)
