# V2 portable build blocker

Status: RESOLVED for portable dependency assets. The user explicitly authorized
one offline restore. It exited 0 using the empty-source configuration and
existing project cache. Subsequent --no-restore publication succeeded with a
seven-file package. Live validation remains NOT RUN.

The attempted command was:

```powershell
& $taskDotnet publish src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:DebugType=None /p:DebugSymbols=false
```

Exit code: 1. NETSDK1047 reports that App/obj/project.assets.json lacks
net10.0-windows/win-x64. Its only target is net10.0-windows. A successful normal
Release build does not prove self-contained publication is possible.

Read-only cache inspection found Microsoft.NETCore.App.Runtime.win-x64,
Microsoft.WindowsDesktop.App.Runtime.win-x64 and Microsoft.NET.ILLink.Tasks
10.0.11, plus PDFsharp 6.2.4 in the source project's existing local cache.
The worktree-local .nuget cache lacks those three runtime/publishing packages.
Package-directory existence alone does not prove the full dependency closure.

An empty-source NuGet.Config was prepared under ignored
artifacts/v2-offline-restore/. The authorized action regenerated
worktree assets from the existing project cache, with all package feeds disabled.
If any dependency is unavailable, stop without downloading/installing it.

Authorized command (executed successfully), from this project's isolated worktree:

```powershell
$taskDotnet = 'E:\Codex\projects\nx-step-launcher\.tools\dotnet\dotnet.exe'
$env:DOTNET_CLI_HOME = 'E:\Codex\projects\nx-pdf-sample\.tools\dotnet-cli-home'
$env:NUGET_PACKAGES = 'E:\Codex\projects\nx-pdf-sample\.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = 'E:\Codex\projects\nx-pdf-sample\.tools\nuget-http-cache'
& $taskDotnet restore src/NxDrawingPdfExporter.App/NxDrawingPdfExporter.App.csproj -r win-x64 --packages $env:NUGET_PACKAGES --configfile artifacts/v2-offline-restore/NuGet.Config /p:SelfContained=true /p:PublishSingleFile=true /p:NuGetAudit=false
```

This changes ignored dependency metadata under the worktree's obj directories
and uses the project's existing cache/CLI directories. It does not change
system environment variables or install SDKs. Subsequent build/test/publish
commands retain --no-restore. No cached package is assumed complete until the
offline resolution succeeds.

## Acceptance status correction

Earlier completion wording for Tasks 4 and 5 was premature. This continuation
added RED-GREEN coverage for root display/persistence, detection concurrency,
specific manual errors and invalid-selection rediscovery. It also completed
the progress/list horizontal docking and parent-container Tab ordering.
These changes are automated evidence only; visual QA and independent review
remain outstanding.

Task 7 package inspection passed; Tasks 8–10 are NOT RUN, and Task 11 is
incomplete. No GUI/NX operations, source-sample changes, commits, tags, pushes,
Issue updates or remote releases were performed for this diagnostic.
