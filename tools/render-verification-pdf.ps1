# Renders every PDF page to PNG using the Windows built-in Windows.Data.Pdf
# component (no installation, no download, no Python). Validation-only tool:
# it never touches the source PDF and writes only into the requested run
# artifacts directory.
#
# Usage (Windows PowerShell 5.1):
#   powershell -NoProfile -File tools\render-verification-pdf.ps1 -PdfPath <pdf> -OutDir <dir> [-Dpi 150]
param(
    [string]$PdfPath = '',
    [string]$OutDir = '',
    [int]$Dpi = 150
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($PdfPath) -or [string]::IsNullOrWhiteSpace($OutDir)) {
    throw '必须提供 -PdfPath 与 -OutDir。'
}

$pdfFullPath = [System.IO.Path]::GetFullPath($PdfPath)
$outFullPath = [System.IO.Path]::GetFullPath($OutDir)
if (-not (Test-Path -LiteralPath $pdfFullPath)) { throw 'PDF 文件不存在。' }

# Renderer identity: the OS built-in Windows.Data.Pdf component.
$pdfDll = Join-Path $env:windir 'System32\Windows.Data.Pdf.dll'
if (-not (Test-Path -LiteralPath $pdfDll)) { throw '未找到系统内置 PDF 渲染组件 Windows.Data.Pdf.dll。' }
$pdfDllVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($pdfDll).FileVersion
$osVersion = [Environment]::OSVersion.Version.ToString()

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Data.Pdf.PdfDocument, Windows.Data.Pdf, ContentType = WindowsRuntime]
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.IRandomAccessStream, Windows.Storage.Streams, ContentType = WindowsRuntime]

# WinRT async helpers for Windows PowerShell 5.1.
$asTaskOperation = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asTaskAction = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' })[0]

function Await-Operation {
    param($WinRtOperation, [Type]$ResultType)
    $netTask = $asTaskOperation.MakeGenericMethod($ResultType).Invoke($null, @($WinRtOperation))
    $null = $netTask.Wait(-1)
    return $netTask.Result
}

function Await-Action {
    param($WinRtAction)
    $netTask = $asTaskAction.Invoke($null, @($WinRtAction))
    $null = $netTask.Wait(-1)
}

New-Item -ItemType Directory -Path $outFullPath -Force | Out-Null

$storageFile = Await-Operation ([Windows.Storage.StorageFile]::GetFileFromPathAsync($pdfFullPath)) ([Windows.Storage.StorageFile])
$pdfDocument = Await-Operation ([Windows.Data.Pdf.PdfDocument]::LoadFromFileAsync($storageFile)) ([Windows.Data.Pdf.PdfDocument])
$pageCount = $pdfDocument.PageCount

$folder = Await-Operation ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync($outFullPath)) ([Windows.Storage.StorageFolder])
$pageInfos = @()
for ($index = 0; $index -lt $pageCount; $index++) {
    $page = $pdfDocument.GetPage($index)
    try {
        $fileName = 'page-{0:D2}.png' -f ($index + 1)
        $options = New-Object Windows.Data.Pdf.PdfPageRenderOptions
        $options.DestinationWidth = [uint32][Math]::Round($page.Size.Width * $Dpi / 96.0)
        $options.DestinationHeight = [uint32][Math]::Round($page.Size.Height * $Dpi / 96.0)
        $outFile = Await-Operation ($folder.CreateFileAsync($fileName, [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])
        $stream = Await-Operation ($outFile.OpenAsync([Windows.Storage.FileAccessMode]::ReadWrite)) ([Windows.Storage.Streams.IRandomAccessStream])
        try {
            Await-Action ($page.RenderToStreamAsync($stream, $options))
        } finally {
            $stream.Dispose()
        }

        $rendered = Get-Item -LiteralPath (Join-Path $outFullPath $fileName)
        $pageInfos += [ordered]@{
            page          = $index + 1
            dipWidth      = [math]::Round($page.Size.Width, 2)
            dipHeight     = [math]::Round($page.Size.Height, 2)
            renderedPng   = $fileName
            renderedBytes = $rendered.Length
        }
    } finally {
        $page.Dispose()
    }
}

$summary = [ordered]@{
    rendererPath     = $pdfDll
    rendererVersion  = $pdfDllVersion
    osVersion        = $osVersion
    requestedDpi     = $Dpi
    pdfPageCount     = $pageCount
    pages            = $pageInfos
}
$summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outFullPath 'render-summary.json') -Encoding UTF8

Write-Host ('RENDER OK: ' + $pageCount + ' page(s) rendered to ' + $outFullPath)
