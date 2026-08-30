# Gate 1 — NX 10 managed worker launch proof

Date: 2026-08-26 (UTC)
Result: **PASS**

## Facts proven

| Requirement | Evidence |
|---|---|
| Launcher exists | `D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe` (file version `6.0.0.0`) |
| Launcher syntax | `run_managed <executable-file> <arguments>` (usage printed with exit code 1 when called without arguments) |
| net48 x64 assembly loads | Probe built for `net48`, `PlatformTarget=x64`, `RuntimeIdentifier=win-x64`; report `processBitness: "x64"` |
| Real CLR runtime | `runtimeVersion: "4.0.30319.42000"` (.NET Framework 4.x CLR) |
| REAL `NXOpen.Session` | `nxSessionAvailable: true` |
| REAL `UFSession` | `ufSessionAvailable: true` |
| Argument echo exact | single argument = report path, echoed verbatim |
| Exit code reliable | probe process exited with code `0`; probe self-recorded `"exitCode": 0` |
| Working directory honored | `currentDirectory` equals the unique run directory under `artifacts\gate-1\` |
| NX root environment | `UGII_ROOT_DIR=D:\Program Files\Siemens\NX 10.0\UGII\` |
| No Siemens binaries copied | `Get-ChildItem bin -Recurse -Include NXOpen*.dll` returns nothing (`Private=false` enforced; guard target verifies file version `10.0.0.24`) |

## Exact command line

```text
"D:\Program Files\Siemens\NX 10.0\UGII\run_managed.exe"
  "E:\Codex\projects\nx-pdf-sample\tools\NxDrawingPdfExporter.Probe\bin\Debug\net48\win-x64\NxDrawingPdfExporter.Probe.exe"
  "<repo>\artifacts\gate-1\<stamp>\probe-report.json"
```

Elapsed wall time: ~4 seconds per run.

## Committed artifacts

- `last-probe-report.json` — the probe's own UTF-8 JSON report from the passing run.
- `last-run-summary.json` — machine-readable harness summary (launcher version, command line, timeout state, elapsed seconds, parsed report).

Raw stdout/stderr remain gitignored under `artifacts\gate-1\<stamp>\`.

## Harness notes

- `tools/run-worker-probe.ps1` builds the probe offline (repo-local NuGet cache), creates a unique
  run directory, launches through `run_managed.exe` only, enforces a watchdog timeout, and writes
  privacy-scrubbed evidence here.
- The probe never opens, imports, or saves any PRT.
