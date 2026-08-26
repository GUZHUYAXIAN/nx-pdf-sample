# NX Drawing PDF Exporter - Project Instructions

## Scope

- Work only inside `E:\Codex\projects\nx-pdf-sample`.
- `E:\Codex\projects\nx-step-launcher` may be inspected read-only for patterns; never edit it.
- Read `README.md` and the complete approved design under `docs/superpowers/specs/` before acting.
- Do not implement product code until an approved implementation plan exists and the user's prompt explicitly authorizes implementation.

## Source and sample safety

- Never save, overwrite, rename, delete, or move the private PRT/PDF samples unless the user explicitly approves that exact operation.
- Never commit `.prt`, `.pdf`, generated PDFs, rendered pages, logs, Siemens DLLs, or private sample metadata.
- Product code must never call NX save APIs for source PRT files.
- Before and after every live NX validation, record SHA-256 for both the drawing PRT and its associated model. Any change is a hard failure.
- Use temporary copies for missing-dependency, corruption, cancellation, and failure-path tests.

## Environment boundaries

- The first supported environment is Windows 11 x64 with Siemens NX 10.0.0.24.
- Verify actual local files and APIs; do not infer compatibility from another NX release.
- Do not modify the NX installation, registry, PATH, system/user environment variables, services, or global configuration.
- Do not redistribute `NXOpen.dll`, `NXOpen.UF.dll`, or any other Siemens binary.
- Product runtime must not download, install, update, or contact external services.
- Do not install SDKs, packages, or tools without explicit user authorization and a reviewable source, location, and impact statement.

## Architecture gates

- Keep the portable WinForms GUI and NX 10 Worker as separate processes.
- The GUI must not load NXOpen assemblies directly.
- The Worker must run through the verified NX 10 `UGII\run_managed.exe` path.
- Complete the minimal Worker launch/session probe before building the full GUI.
- If the probe cannot obtain a real NX Session and UFSession, stop and report evidence; do not simulate or bypass the gate.
- Process PRT files sequentially. Do not parallelize NX sessions or exports.

## Behavioral requirements

- A sheet is exportable only when it contains at least one actual drafting view.
- Pages containing only the predefined border, title block, general technical requirements, or tables are skipped.
- Missing model dependencies or drafting-view update failures fail that PRT; they are not treated as blank pages.
- Preserve the NX drawing navigator order. Do not alphabetically sort sheet names.
- Preserve the complete source basename when changing `.prt` to `.pdf`.
- Existing-output choices are mutually exclusive: skip (default) or safe overwrite.
- Safe overwrite must preserve the old PDF until the new temporary PDF passes validation.
- Never publish a partial PDF as the official output.

## Engineering and evidence

- Use small, reviewable commits; do not push or create releases.
- Use tests before implementation for deterministic logic.
- Do not swallow exceptions or report success without artifacts.
- Separate unit-test evidence, NX injection/launch evidence, and true PDF visual-QA evidence.
- Completion requires tests, build output, live NX evidence, rendered PDF inspection, unchanged source hashes, GUI DPI screenshots, and clean-extraction verification.
- Keep known limitations explicit. Do not claim support for untested NX versions or machines.
