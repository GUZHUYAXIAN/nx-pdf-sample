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
    [string]$ReleaseRoot
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = Join-Path $repoRoot '.tools\dotnet-cli-home' }
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
if (-not $PackageCache) { $PackageCache = Join-Path $repoRoot '.tools\nuget-packages' }
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
& $taskDotnet publish (Join-Path $repoRoot 'src\NxDrawingPdfExporter.App\NxDrawingPdfExporter.App.csproj') -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:DebugType=None /p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'GUI 发布失败。' }

$guiPublish = Join-Path $repoRoot 'src\NxDrawingPdfExporter.App\bin\Release\net10.0-windows\win-x64\publish'
$guiExe = Join-Path $guiPublish 'NxDrawingPdfExporter.App.exe'
if (-not (Test-Path $guiExe)) { throw '未找到 GUI 单文件发布输出。' }
Copy-Item $guiExe (Join-Path $packageRoot 'NX图纸批量导出工具.exe')

Write-Host '=== 复制 Worker（net48 x64） ==='
$workerOut = Get-ChildItem (Join-Path $repoRoot 'src\NxDrawingPdfExporter.Worker\bin\Release') -Recurse -Filter 'NxDrawingPdfExporter.Worker.exe' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $workerOut) { throw '未找到 Worker Release 输出。' }
$workerDir = $workerOut.DirectoryName
Get-ChildItem $workerDir -File | Where-Object { $_.Extension -ne '.pdb' } | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $packageRoot ('worker\' + $_.Name))
}

Copy-Item (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt') (Join-Path $packageRoot 'THIRD-PARTY-NOTICES.txt')

@'
NX 图纸批量导出工具（便携版）

1. 双击 NX图纸批量导出工具.exe 运行；无需安装任何运行库。
2. 仅支持 Windows 11 x64 与 Siemens NX 10.0.0.24；程序会自动检测已安装的 NX，无法确定时可手动选择 NX 根目录。
3. 选择扫描文件夹或手动多选 PRT，选择输出方式后执行预检查，再开始导出。
4. 导出过程逐个处理文件；导出的 PDF 会先通过校验再正式发布。
5. 本工具不联网、不修改源 PRT、不修改 NX 安装和系统配置。
'@ | Set-Content -LiteralPath (Join-Path $packageRoot '说明.txt') -Encoding UTF8

Write-Host 'PUBLISH OK'
Write-Host ('PACKAGE ROOT: ' + $packageRoot)
