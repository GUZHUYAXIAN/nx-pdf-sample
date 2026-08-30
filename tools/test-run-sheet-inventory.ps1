# Harness self-test for tools/run-sheet-inventory.ps1 (Task 6 gaps 1-2).
# Exercises the report-validation function, the kill-only-our-process
# semantics, and the hash snapshot capture with disposable fixtures under
# gitignored artifacts/. It never launches NX and never touches samples.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$harnessPath = Join-Path $PSScriptRoot 'run-sheet-inventory.ps1'
. $harnessPath -SelfTestOnly

$fixtureRoot = Join-Path $repoRoot ('artifacts\gate-2-harness-selftest\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

$failures = New-Object System.Collections.Generic.List[string]
$checks = 0

function Assert-HarnessCondition {
    param([bool]$Condition, [string]$Name)
    $script:checks++
    if (-not $Condition) {
        $script:failures.Add($Name) | Out-Null
        Write-Host ('FAIL: ' + $Name)
    } else {
        Write-Host ('pass: ' + $Name)
    }
}

function New-ReportFixture {
    param([hashtable]$Override, [string]$Name)
    $report = @{
        Success = $true
        Failure = $null
        LoadDiagnostics = @()
        Sheets = @(
            @{
                ExportOrderIndex = 0
                DraftingViewCount = 2
                Length = 420
                Height = 594
                Units = 'Millimeters'
                NameToken = '0123456789abcdef'
                NativeIndex = 0
            },
            @{
                ExportOrderIndex = 1
                DraftingViewCount = 0
                Length = 297
                Height = 420
                Units = 'Millimeters'
                NameToken = 'fedcba9876543210'
                NativeIndex = 1
            }
        )
    }
    foreach ($key in $Override.Keys) { $report[$key] = $Override[$key] }
    $path = Join-Path $fixtureRoot $Name
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

# --- report validation: reject every unusable shape ---
$missing = Join-Path $fixtureRoot 'does-not-exist.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $missing)) 'missing report is rejected'

$emptyPath = Join-Path $fixtureRoot 'empty.json'
Set-Content -LiteralPath $emptyPath -Value '' -Encoding UTF8
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $emptyPath)) 'empty report is rejected'

$malformedPath = Join-Path $fixtureRoot 'malformed.json'
Set-Content -LiteralPath $malformedPath -Value '{ not json' -Encoding UTF8
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $malformedPath)) 'malformed JSON is rejected'

$notSuccessful = New-ReportFixture -Override @{ Success = $false; Failure = '注入的失败' } -Name 'failed.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $notSuccessful)) 'unsuccessful report is rejected'

$noSheets = New-ReportFixture -Override @{ Sheets = @() } -Name 'no-sheets.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $noSheets)) 'report without sheet facts is rejected'

$badIndex = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 1; DraftingViewCount = 1; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 0 }
    )
} -Name 'bad-index.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $badIndex)) 'export order index mismatching array position is rejected'

$badNativeIndex = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = 1; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 7 }
    )
} -Name 'bad-native-index.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $badNativeIndex)) 'native index mismatching array position is rejected'

$duplicateExportOrderIndex = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = 1; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 0 },
        @{ ExportOrderIndex = 0; DraftingViewCount = 0; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = 'fedcba9876543210'; NativeIndex = 1 }
    )
} -Name 'duplicate-export-order-index.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $duplicateExportOrderIndex)) 'duplicate export order indices are rejected'

$negativeViews = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = -1; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 0 }
    )
} -Name 'negative-views.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $negativeViews)) 'negative drafting view count is rejected'

$zeroSize = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = 1; Length = 0; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 0 }
    )
} -Name 'zero-size.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $zeroSize)) 'zero sheet size is rejected'

$noUnits = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = 1; Length = 420; Height = 594; Units = ''; NameToken = '0123456789abcdef'; NativeIndex = 0 }
    )
} -Name 'no-units.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $noUnits)) 'missing units are rejected'

$badToken = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = 1; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = 'ZZZZ'; NativeIndex = 0 }
    )
} -Name 'bad-token.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $badToken)) 'malformed name token is rejected'

$duplicateToken = New-ReportFixture -Override @{
    Sheets = @(
        @{ ExportOrderIndex = 0; DraftingViewCount = 1; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 0 },
        @{ ExportOrderIndex = 1; DraftingViewCount = 0; Length = 420; Height = 594; Units = 'Millimeters'; NameToken = '0123456789abcdef'; NativeIndex = 1 }
    )
} -Name 'duplicate-token.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $duplicateToken)) 'duplicate name tokens are rejected'

$successWithFailure = New-ReportFixture -Override @{ Failure = '矛盾状态' } -Name 'success-with-failure.json'
Assert-HarnessCondition ($null -ne (Get-InventoryReportIssue -ReportPath $successWithFailure)) 'success report carrying failure text is rejected'

$valid = New-ReportFixture -Name 'valid.json'
Assert-HarnessCondition ($null -eq (Get-InventoryReportIssue -ReportPath $valid)) 'structurally complete success report is accepted'

# --- Stop-LaunchedProcess: only the process we started is terminated ---
$survivor = Start-Process -FilePath 'powershell' -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 180') -PassThru -WindowStyle Hidden
$victim = Start-Process -FilePath 'powershell' -ArgumentList @('-NoProfile', '-Command', 'Start-Sleep -Seconds 180') -PassThru -WindowStyle Hidden
$stopError = Stop-LaunchedProcess -Process $victim
Assert-HarnessCondition ($null -eq $stopError) 'Stop-LaunchedProcess waits for the victim to exit'
Assert-HarnessCondition ($victim.HasExited) 'victim process is terminated'
Assert-HarnessCondition (-not $survivor.HasExited) 'unrelated process is left running'
try {
    Stop-LaunchedProcess -Process $survivor | Out-Null
} catch {
    # cleanup best effort; recorded only if it fails after the checks
    $failures.Add('survivor cleanup failed') | Out-Null
}

# --- Write-HashSnapshot: captures both hashes and writes the artifact ---
$hashFixtureDir = Join-Path $fixtureRoot 'hashes'
New-Item -ItemType Directory -Path $hashFixtureDir -Force | Out-Null
$drawingCopy = Join-Path $hashFixtureDir 'drawing.prt'
$modelCopy = Join-Path $hashFixtureDir 'model.prt'
Set-Content -LiteralPath $drawingCopy -Value 'disposable drawing fixture bytes' -Encoding UTF8
Set-Content -LiteralPath $modelCopy -Value 'disposable model fixture bytes' -Encoding UTF8
$snapshot = Write-HashSnapshot -DrawingPath $drawingCopy -ModelPath $modelCopy -RunDir $hashFixtureDir -Phase 'selftest'
Assert-HarnessCondition ($snapshot.drawing -eq (Get-FileHash -LiteralPath $drawingCopy -Algorithm SHA256).Hash) 'drawing hash matches Get-FileHash'
Assert-HarnessCondition ($snapshot.model -eq (Get-FileHash -LiteralPath $modelCopy -Algorithm SHA256).Hash) 'model hash matches Get-FileHash'
Assert-HarnessCondition (Test-Path -LiteralPath (Join-Path $hashFixtureDir 'source-hashes-selftest.json')) 'hash snapshot artifact is written'

Remove-Item -LiteralPath $fixtureRoot -Recurse -Force

Write-Host ('harness self-test checks: ' + $checks + ', failures: ' + $failures.Count)
if ($failures.Count -gt 0) { exit 1 }
exit 0
