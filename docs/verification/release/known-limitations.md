# Known Limitations (V1)

1. **NX version and machine scope.** Only Windows 11 x64 with Siemens NX
   10.0.0.24 at the verified install root (`D:\Program Files\Siemens\NX
   10.0`) is supported. The GUI fails closed for any other version or
   location. No other NX release, machine, or locale has been tested.

2. **Export order contract.** V1 exports sheets in the order returned by the
   public NXOpen `workPart.DrawingSheets` collection. The visible Part/Drawing
   Navigator's timestamp/dependency display sorting is a GUI preference with
   no supporting public API in NX 10 and is intentionally not matched.

3. **Exportable-sheet rule.** A sheet exports only when
   `GetDraftingViews().Length >= 1`. Border/title-block/technical-requirement
   template pages are skipped. The file-level no-valid-sheet behavior is now
   proven on real NX: Gate 3 scenario S11 (2026-08-30) uses a dedicated
   synthetic PRT — one A4 sheet, zero drafting views, created by
   `tools/NxDrawingPdfExporter.FixtureBuilder` — and the worker reports
   `NoValidSheets` with no PDF and no temp leftovers, then continues to a
   real successful export.

4. **Missing dependencies fail the PRT.** Any unloaded component fails that
   PRT with NX's load diagnostics in the message; it is never exported with
   stale views and never treated as a blank page.

5. **`run_managed.exe` exit codes are not authoritative.** NX's launcher can
   return 0 even when the worker reports failure. The `result.json` /
   export report validation is the only success evidence; harnesses and the
   GUI treat it accordingly.

6. **DPI screenshots.** GUI visual QA was performed at the machine's current
   100% scaling only. 125%/150% screenshots require temporarily changing the
   system display scaling, which is a machine configuration change outside
   the authorized boundaries. Mitigations in place: PerMonitorV2 manifest and
   DPI-aware `AutoScaleMode`; group-level layout uses
   `TableLayoutPanel`/`FlowLayoutPanel`/`AutoSize`. However, several inner
   controls (text boxes, list views, buttons) still carry fixed pixel
   `Left`/`Top`/`Width`/`Height` values, so clipping or overlap at higher
   scaling factors — especially with Chinese text and long paths — is an
   untested risk. The user accepted this as a v1.0.0 release limitation and
   deferred the remaining visual QA to GitHub Issue #2; refactor to
   docking/anchored layouts only if that QA exposes defects.

7. **Offline verification.** The machine's network was not disabled during
   QA (system configuration boundary). The offline claim rests on a source
   audit: product code contains no networking APIs, and the entire
   build/publish/test pipeline ran against the repo-local NuGet cache
   without network access. The user accepted this as a v1.0.0 release
   limitation and deferred the physical offline run to GitHub Issue #3.

8. **Single NX session, sequential processing.** Files are processed one at
   a time in one NX session started through `run_managed.exe`. Cancellation
   takes effect at file boundaries; the current atomic operation always
   finishes first.

9. **V1 scope only.** No STEP/DWG/DXF/CGM/3D-PDF output, no Teamcenter/PDM,
   no folder watching, no parallel exports, no drawing editing, and no
   installer or auto-update. Product runtime never contacts the network.
