param(
    [Parameter(Mandatory=$true)][string]$DotnetPath,
    [Parameter(Mandatory=$true)][string]$Nx10Root,
    [Parameter(Mandatory=$true)][string]$Nx2506Root,
    [Parameter(Mandatory=$true)][string]$Drawing,
    [Parameter(Mandatory=$true)][string]$Model,
    [Parameter(Mandatory=$true)][string]$ReferencePdf,
    [Parameter(Mandatory=$true)][string]$EvidenceRoot,
    [string]$PackageRoot,
    [switch]$RequireProgress
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$artifacts = [IO.Path]::GetFullPath((Join-Path $repo 'artifacts')) + '\'
$EvidenceRoot = [IO.Path]::GetFullPath($EvidenceRoot)
if (-not $EvidenceRoot.StartsWith($artifacts, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $EvidenceRoot)) {
    throw 'Select a new directory under project artifacts.'
}
[void](New-Item -ItemType Directory -Path $EvidenceRoot)
$driver = Join-Path $repo 'tools\NxDrawingPdfExporter.Gate3Driver\bin\Release\net10.0-windows\NxDrawingPdfExporter.Gate3Driver.dll'
$pdfGate = Join-Path $repo 'tools\NxDrawingPdfExporter.PdfGate\bin\Release\net10.0-windows\NxDrawingPdfExporter.PdfGate.dll'
$worker10 = if ($PackageRoot) { Join-Path $PackageRoot 'worker\nx10\NxDrawingPdfExporter.Worker.exe' } else {
    Join-Path $repo 'src\NxDrawingPdfExporter.Worker\bin\Release\net48\win-x64\NxDrawingPdfExporter.Worker.exe'
}
$worker2506 = if ($PackageRoot) { Join-Path $PackageRoot 'worker\nx2506\NxDrawingPdfExporter.Worker.Nx2506.exe' } else {
    Join-Path $repo 'src\NxDrawingPdfExporter.Worker.Nx2506\bin\Release\net48\win-x64\NxDrawingPdfExporter.Worker.Nx2506.exe'
}
$drawName = [IO.Path]::GetFileName($Drawing)
$modelName = [IO.Path]::GetFileName($Model)
$stem = [IO.Path]::GetFileNameWithoutExtension($drawName)

function Get-ShaRecord([string[]]$Files) {
    @($Files | ForEach-Object {
        [pscustomobject]@{ path=$_; sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
    })
}

function Invoke-Scenario($profile, $name, [string[]]$copies, [string]$mode, [string]$routing, [string]$destination,
    [string]$policy, [string[]]$statuses, [string[]]$expectedNames, [string[]]$expectedDirectories) {
    $folder = Join-Path $EvidenceRoot ($profile.Id + '-' + $name)
    [void](New-Item -ItemType Directory -Path $folder)
    $tracked = @($Drawing, $Model) + $copies
    $before = ConvertTo-Json -InputObject (Get-ShaRecord $tracked) -Depth 3
    [IO.File]::WriteAllText((Join-Path $folder 'hashes-before.json'), $before)
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = $DotnetPath
    $inputs = @($copies | Where-Object { [IO.Path]::GetFileName($_) -ceq $drawName })
    if ($name -ne 'zero-grouped' -and $name -ne 'cancel') { $inputs = @($inputs[0]) }
    $arguments = @($driver,$mode,($inputs -join ';'),$routing,$destination,$policy,(Join-Path $folder 'jobs'))
    $psi.Arguments = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    $psi.WorkingDirectory = $folder
    $psi.EnvironmentVariables['NXPDF_NX_ROOT'] = $profile.Root
    $psi.EnvironmentVariables['NXPDF_WORKER_EXE'] = $profile.Worker
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    try {
        if (-not $process.Start()) { throw 'Driver could not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(900000)) { throw "Driver timed out. Stop NX validation. Evidence: $folder" }
        [IO.File]::WriteAllText((Join-Path $folder 'host.json'), $stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText((Join-Path $folder 'host-stderr.txt'), $stderr.GetAwaiter().GetResult())
        $exitCode = $process.ExitCode
    }
    finally {
        $after = ConvertTo-Json -InputObject (Get-ShaRecord $tracked) -Depth 3
        [IO.File]::WriteAllText((Join-Path $folder 'hashes-after.json'), $after)
        $process.Dispose()
        if ($before -cne $after) { throw "HARD FAILURE: drawing or model SHA-256 changed. Evidence: $folder" }
    }

    $hostResult = Get-Content -LiteralPath (Join-Path $folder 'host.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $hostResult.runDirectory) { throw "No Worker run directory. Stop NX validation. Evidence: $folder" }
    $stdout = Get-Content -LiteralPath (Join-Path $hostResult.runDirectory 'worker-stdout.txt') -Raw
    if (-not $stdout.Contains('Session=True UFSession=True AssemblyOrigins=True')) {
        throw "Real NX session/assembly gate failed. Stop NX validation. Evidence: $folder"
    }
    if ($exitCode -ne 0 -or ($hostResult.results.status -join ',') -cne ($statuses -join ',')) {
        throw "Unexpected Worker or publication result. Evidence: $folder"
    }

    if ($RequireProgress) {
        $snapshotPath = Join-Path $hostResult.runDirectory 'progress.json'
        $snapshot = Get-Content -LiteralPath $snapshotPath -Raw | ConvertFrom-Json
        $timeline = @($hostResult.phaseTimeline)
        $exports = @($timeline | Where-Object { $_.Phase -eq 3 -and $null -ne $_.CurrentItemIndex })
        $cumulative = @($timeline | Where-Object { $_.Phase -eq 3 -and $_.Step -ne 9 -and $_.Export.Processed -gt 0 -and $_.Export.Processed -lt $_.Export.Total })
        $saving = @($timeline | Where-Object Phase -eq 5)
        $savedExpected = @($statuses | Where-Object { $_ -in @('Success','Overwritten') }).Count
        if (!$snapshot -or $snapshot.SchemaVersion -ne 1 -or $snapshot.Stage -ne 9 -or
            $snapshot.Total -ne $inputs.Count -or $snapshot.Processed -ne $savedExpected -or
            $snapshot.Generated -ne $savedExpected -or $saving.Count -eq 0 -or
            $saving[0].Publication.Total -ne $savedExpected -or $saving[0].Publication.Processed -ne 0 -or
            $saving[-1].Publication.Saved -ne $savedExpected) {
            throw "Unexpected phase counters or terminal progress snapshot. Evidence: $folder"
        }
        if ($name -eq 'zero-grouped' -and ($exports.Count -eq 0 -or $cumulative.Count -eq 0)) {
            throw "Missing live active-file or cumulative Worker progress. Evidence: $folder"
        }
    }
    $records = @()
    $index = 0
    foreach ($item in @($hostResult.results | Where-Object status -In @('Success','Overwritten'))) {
        $index++
        if ([IO.Path]::GetFileName($item.target) -cne $expectedNames[$index-1] -or
            [IO.Path]::GetDirectoryName($item.target) -cne $expectedDirectories[$index-1]) {
            throw "Unexpected published target. Evidence: $folder"
        }
        $inspectionPath = Join-Path $folder "pdf-$index.json"
        & $DotnetPath $pdfGate inspect $item.target $inspectionPath | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "PDF inspection failed. Evidence: $folder" }
        $inspection = Get-Content -LiteralPath $inspectionPath -Raw | ConvertFrom-Json
        if (-not $inspection.HasPdfHeader -or $inspection.PageCount -ne $item.exportedSheetCount) {
            throw "PDF header or page count mismatch. Evidence: $folder"
        }
        & (Join-Path $PSScriptRoot 'render-verification-pdf.ps1') -PdfPath $item.target -OutDir (Join-Path $folder "pdf-$index-render")
        $records += [pscustomobject]@{ target=$item.target; pages=$inspection.PageCount; dimensions=$inspection.PageSizes }
    }
    if ($index -ne $expectedNames.Count) { throw "Published output count mismatch. Evidence: $folder" }
    if ($name -eq 'cancel' -and (Test-Path -LiteralPath $hostResult.results[1].target)) {
        throw "Cancelled item was published. Evidence: $folder"
    }
    [pscustomobject]@{
        nx=$profile.Id; scenario=$name; results=$statuses; session=$true; hashesUnchanged=$true; pdf=$records
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $folder 'summary.json') -Encoding UTF8
    Write-Host ("PASS " + $profile.Id + " " + $name)
}

& (Join-Path $PSScriptRoot 'render-verification-pdf.ps1') -PdfPath $ReferencePdf -OutDir (Join-Path $EvidenceRoot 'reference-render')
$profiles = @(@{ Id='nx10'; Root=$Nx10Root; Worker=$worker10 }, @{ Id='nx2506'; Root=$Nx2506Root; Worker=$worker2506 })
foreach ($profile in $profiles) {
    $fixture = Join-Path $EvidenceRoot ($profile.Id + '-fixtures')
    $first = Join-Path $fixture 'a'
    $second = Join-Path $fixture 'b'
    $out = Join-Path $fixture 'out'
    $cancelOut = Join-Path $fixture 'cancel-out'
    foreach ($path in @($first, $second, $out, $cancelOut)) { [void](New-Item -ItemType Directory -Path $path) }
    $copies = @(foreach ($path in @($first,$second)) {
        Copy-Item -LiteralPath $Drawing -Destination $path
        Copy-Item -LiteralPath $Model -Destination $path
        Join-Path $path $drawName
        Join-Path $path $modelName
    })
    $targetA = Join-Path $out 'group-1'
    $targetB = Join-Path $out 'group-2'
    $zero = $stem + '.pdf'
    Invoke-Scenario $profile 'zero-grouped' $copies 'manual' 'grouped' $out 'revision' @('Success','Success') @($zero,$zero) @($targetA,$targetB)
    Copy-Item -LiteralPath (Join-Path $targetA $zero) -Destination (Join-Path $first ($stem + '-R100.pdf'))
    Invoke-Scenario $profile 'new-revision' $copies 'manual' 'unified' $targetA 'revision' @('Success') @($stem + '-R101.pdf') @($targetA)
    Invoke-Scenario $profile 'old-revision' $copies 'manual' 'unified' $targetA 'revision:0' @('Overwritten') @($zero) @($targetA)
    Invoke-Scenario $profile 'cancel' $copies 'cancel' 'grouped' $cancelOut 'revision' @('Success','Cancelled') @($stem + '-R101.pdf') @(Join-Path $cancelOut 'group-1')
}
