using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Session;
using NxDrawingPdfExporter.App.Progress;
using NxDrawingPdfExporter.App.Localization;
using NxDrawingPdfExporter.App.Logging;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Input;
using NxDrawingPdfExporter.Core.Jobs;
using NxDrawingPdfExporter.Core.Output;
using NxDrawingPdfExporter.Core.Pdf;

namespace NxDrawingPdfExporter.App
{
    public enum GuiInputMode
    {
        ScanFolder = 0,
        ManualSelection = 1
    }

    /// <summary>控制器依赖集合；全部可注入，测试无需真实 NX 或网络。</summary>
    public sealed class ApplicationServices
    {
        public IInputDiscoveryService Discovery { get; init; } = new InputDiscoveryService(new PhysicalFileSystem());

        public ITargetStateReader TargetStateReader { get; init; } = new PhysicalTargetStateReader();

        public IOutputDirectoryReader DirectoryReader { get; init; } = new PhysicalOutputDirectoryReader();

        public IWorkerProcessLauncher WorkerLauncher { get; init; } = new WorkerProcessLauncher();

        public IPdfInspector PdfInspector { get; init; } = new Pdf.PdfSharpInspector();

        public IFileReplacer FileReplacer { get; init; } = new FileReplacer();

        public INxInstallationDiscoveryService NxInstallationDiscovery { get; init; } =
            new NxInstallationDiscoveryService(
                new INxInstallationCandidateSource[]
                {
                    new RegistryNxCandidateSource(),
                    new InstalledApplicationNxCandidateSource(),
                    new EnvironmentNxCandidateSource(),
                },
                new NxInstallationValidator());

        public INxSettingsStore NxSettings { get; init; } = JsonNxSettingsStore.CreateDefault();

        /// <summary>运行目录根；默认 %LOCALAPPDATA%\NxDrawingPdfExporter\runs。</summary>
        public Func<string>? RunRootProvider { get; init; }

        /// <summary>Worker 程序路径；默认相对程序目录的 worker\NxDrawingPdfExporter.Worker.exe。</summary>
        public string? WorkerExePath { get; init; }

    }

    /// <summary>
    /// 无 UI 依赖的应用控制器：模式状态、预检查、运行编排、结果发布与取消。
    /// WinForms 窗体只是它的薄绑定层。
    /// </summary>
    public sealed partial class ApplicationController
    {
        private readonly ApplicationServices services;
        private readonly ILogSink log;
        private readonly ILogSink nxDiagnosticLog;
        private string? cancellationFlagPath;
        private bool explicitlyReviewed;

        public ApplicationController(ApplicationServices services, ILogSink log, ILogSink? nxDiagnosticLog = null)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.nxDiagnosticLog = nxDiagnosticLog ?? log;
            Session = new PreflightSession(new RevisionOutputPlanner(
                new RevisionCatalog(services.DirectoryReader), services.TargetStateReader));
        }

        // ---- 用户可选状态（默认值来自批准的设计） ----
        public GuiInputMode InputMode { get; set; } = GuiInputMode.ScanFolder;

        public string ScanFolderPath { get; set; } = "";

        public bool IncludeSubfolders { get; set; }

        public IReadOnlyList<string> ManualPaths { get; set; } = Array.Empty<string>();

        public OutputMode OutputMode { get; set; } = OutputMode.GroupedDirectories;

        public Dictionary<string, string> GroupTargets { get; } = new(StringComparer.OrdinalIgnoreCase);

        public PreflightSession Session { get; }

        public string UnifiedOutputDirectory { get; set; } = "";

        public ExistingPdfPolicy ExistingPdfPolicy { get; set; } = ExistingPdfPolicy.Skip;

        // ---- 运行时状态 ----
        public NxInstallationDiscoveryResult NxDiscovery { get; private set; } =
            new(Array.Empty<NxInstallation>(), Array.Empty<NxDetectionIssue>(), null);

        public NxInstallation? SelectedNx => NxDiscovery.SelectedInstallation;

        public bool IsNxDetectionBusy { get; private set; }

        private Func<string>? nxMessage;
        public string NxStatusMessage => nxMessage?.Invoke() ?? Text.Get("NxNotChecked");

        public bool IsBusy { get; private set; }

        public UiText Text { get; } = new();

        private (string Key, object[] Arguments) progress = ("Ready", []);
        public string ProgressText => Text.Get(progress.Key, progress.Arguments);
        private void SetProgress(string key, params object[] arguments) => progress = (key, arguments);

        public IReadOnlyList<FileResult> Results { get; private set; } = Array.Empty<FileResult>();

        public ResultSummary? Summary { get; private set; }

        public string SummaryText => Summary is { } summary ? Text.Get("Summary", summary.Total,
            summary.Succeeded, summary.Overwritten, summary.SkippedExisting, summary.PureModel,
            summary.NoValidSheets, summary.NameConflicts, summary.Cancelled, summary.Failed) : Text.Get("NoRun");

        public string? CurrentRunDirectory { get; private set; }

        public string? InputTechnicalDetails { get; private set; }

        public event Action? StateChanged;

        private void Raise() => StateChanged?.Invoke();

        public async Task RefreshNxStatusAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy || IsNxDetectionBusy)
            {
                throw new InvalidOperationException("运行期间不能重新检测 NX。");
            }

            IsNxDetectionBusy = true;
            nxMessage = () => Text.Get("NxDetecting");
            Raise();
            try
            {
                NxSettingsLoadResult settings = await services.NxSettings.LoadAsync(cancellationToken);
                NxDiscovery = await services.NxInstallationDiscovery.DetectAsync(
                    settings.Settings.NxRootDirectory,
                    cancellationToken);
                nxMessage = () => FormatNxStatus(NxDiscovery, settings.Status);
                if (SelectedNx is { } selected)
                    await SaveSelectedNxAsync(selected, cancellationToken);
                LogNxIssues(NxDiscovery.Issues);
            }
            finally
            {
                IsNxDetectionBusy = false;
                Raise();
            }
        }

        public async Task SelectDetectedNxAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            if (IsBusy || IsNxDetectionBusy)
            {
                throw new InvalidOperationException("当前不能切换 NX 安装。 ");
            }

            NxInstallation? selected = NxDiscovery.Installations.FirstOrDefault(installation =>
                string.Equals(installation.RootDirectory, rootDirectory, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                throw new ArgumentException("所选 NX 安装不在当前检测结果中。", nameof(rootDirectory));
            }

            IsNxDetectionBusy = true;
            Raise();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                NxDiscovery = NxDiscovery with { SelectedInstallation = selected };
                nxMessage = () => FormatNxStatus(NxDiscovery, NxSettingsLoadStatus.Loaded);
                await SaveSelectedNxAsync(selected, cancellationToken);
            }
            finally
            {
                IsNxDetectionBusy = false;
                Raise();
            }
        }

        public async Task SelectManualNxAsync(string rootDirectory, CancellationToken cancellationToken = default)
        {
            if (IsBusy || IsNxDetectionBusy)
            {
                throw new InvalidOperationException("当前不能切换 NX 安装。");
            }

            IsNxDetectionBusy = true;
            Raise();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                NxInstallationDiscoveryResult validated = services.NxInstallationDiscovery.ValidateManual(rootDirectory);
                if (validated.SelectedInstallation is null)
                {
                    nxMessage = () => Text.Get("NxManualInvalid") + " " + FormatNxIssues(validated.Issues);
                    LogNxIssues(validated.Issues);
                    return;
                }

                NxDiscovery = validated with
                {
                    Installations = NxDiscovery.Installations
                        .Where(i => !string.Equals(i.RootDirectory, validated.SelectedInstallation.RootDirectory, StringComparison.OrdinalIgnoreCase))
                        .Append(validated.SelectedInstallation).ToArray()
                };
                nxMessage = () => FormatNxStatus(NxDiscovery, NxSettingsLoadStatus.Loaded);
                await SaveSelectedNxAsync(validated.SelectedInstallation, cancellationToken);
                LogNxIssues(validated.Issues);
            }
            finally
            {
                IsNxDetectionBusy = false;
                Raise();
            }
        }

        private void LogNxIssues(IReadOnlyList<NxDetectionIssue> issues)
        {
            try
            {
                foreach (NxDetectionIssue issue in issues)
                    nxDiagnosticLog.Write($"NX detection: code={issue.Code}; source={issue.Source}; exception={issue.Diagnostic}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                var previous = nxMessage;
                nxMessage = () => previous?.Invoke() + Text.Get("NxLogFailed");
            }
        }

        private async Task SaveSelectedNxAsync(NxInstallation selected, CancellationToken cancellationToken)
        {
            try
            {
                await services.NxSettings.SaveAsync(new NxSettings(1, selected.RootDirectory), cancellationToken);
            }
            catch (IOException)
            {
                nxMessage = () => Text.Get("NxSaveFailed");
                Raise();
            }
            catch (UnauthorizedAccessException)
            {
                nxMessage = () => Text.Get("NxSaveFailed");
                Raise();
            }
        }

        /// <summary>输出预检查列表，供 GUI 预检查网格显示；不执行任何导出。</summary>
        public OutputPlan Preflight()
        {
            UpdatePreflightInputs();
            explicitlyReviewed = true;
            return Session.Refresh();
        }

        public void UpdatePreflightInputs()
        {
            var discovered = DiscoverSources(InputMode, ScanFolderPath, IncludeSubfolders, ManualPaths);
            UpdatePreflightInputs(discovered);
        }

        private void UpdatePreflightInputs((IReadOnlyList<string> Sources, string? Error) discovered)
        {
            InputTechnicalDetails = discovered.Error;
            if (discovered.Error is { } error)
            {
                log.Write("输入收集失败: " + SingleLine(error));
                SetProgress("InputFailed");
            }
            Session.Update(discovered.Sources,
                new OutputRoutingSettings(OutputMode, UnifiedOutputDirectory, GroupTargets), ExistingPdfPolicy);
        }

        public async Task RunAsync()
        {
            if (IsBusy || IsNxDetectionBusy)
            {
                throw new InvalidOperationException("已有任务正在运行。");
            }

            if (SelectedNx is { } selected
                && !SameRuntime(selected, services.NxInstallationDiscovery.ValidateManual(selected.RootDirectory).SelectedInstallation))
            {
                NxDiscovery = new(Array.Empty<NxInstallation>(), Array.Empty<NxDetectionIssue>(), null);
                await RefreshNxStatusAsync();
                SetProgress("NxChanged");
                Raise();
                return;
            }

            IsBusy = true;
            // 每次新运行从干净状态开始：上一次运行的结果、汇总、进度与
            // 运行目录不得在本次运行失败时残留在界面上。
            Results = Array.Empty<FileResult>();
            Summary = null;
            SetProgress("Preparing");
            CurrentRunDirectory = null;
            runClock = System.Diagnostics.Stopwatch.StartNew();
            publicationCancellation = false;
            UpdateProgress(_ => new RunProgressState { Generation = ++runGeneration, Phase = RunPhase.Preparing });
            Raise();
            try
            {
                if (!Progress.CancelRequested)
                    await RunCore();
            }
            finally
            {
                IsBusy = false;
                cancellationFlagPath = null;
                runClock?.Stop();
                UpdateProgress(state => state with
                {
                    Phase = progress.Key is "Stopping" or "WorkerCancelled" or "PublishStopped" ? RunPhase.Cancelled :
                        progress.Key is "Completed" or "AllSkipped" ? RunPhase.Finished : RunPhase.Failed,
                    CurrentItemIndex = null, CurrentSourcePath = null
                });
                Raise();
            }
        }

        /// <summary>请求取消：创建取消标志文件；绝不终止正在进行的原子操作。</summary>
        public void Cancel()
        {
            lock (progressGate)
            {
                if (!IsBusy || currentProgress.CancelRequested)
                    return;
                publicationCancellation = currentProgress.Phase == RunPhase.Publishing;
                if (currentProgress.Phase is RunPhase.Initializing or RunPhase.Exporting && cancellationFlagPath is { } path)
                    File.WriteAllText(path, "cancel");
                SetProgress(publicationCancellation ? "PublishStopping" : "Stopping");
                Volatile.Write(ref currentProgress, currentProgress with { CancelRequested = true });
            }
            ProgressChanged?.Invoke();
        }

        private async Task RunCore()
        {
            NxInstallation? selectedNx = SelectedNx;
            if (selectedNx is null)
            {
                SetProgress("NxRequired");
                return;
            }

            string launcherPathForRun = selectedNx.LauncherPath;

            var onUiThread = SynchronizationContext.Current is WindowsFormsSynchronizationContext;
            var inputMode = InputMode;
            var scanFolder = ScanFolderPath;
            var includeSubfolders = IncludeSubfolders;
            var manualPaths = ManualPaths.ToArray();
            var discovered = onUiThread
                ? await Task.Run(() => DiscoverSources(inputMode, scanFolder, includeSubfolders, manualPaths))
                : DiscoverSources(inputMode, scanFolder, includeSubfolders, manualPaths);
            if (Progress.CancelRequested)
                return;
            UpdatePreflightInputs(discovered);
            if (Session.Inputs.Count == 0)
            {
                SetProgress(InputTechnicalDetails is null ? "NoInput" : "InputFailed");
                return;
            }

            var reviewed = explicitlyReviewed ? Session.CurrentPlan! :
                onUiThread ? await Session.RefreshAsync() : Session.Refresh();
            if (Progress.CancelRequested)
                return;
            if (Session.IsStale || Session.HasInvalidEdit)
            {
                SetProgress("ReviewNeeded");
                return;
            }
            if (reviewed.HasBlockingIssues)
            {
                Results = reviewed.Items.Where(item => item.IsBlocking).Select(item => PlanResult(item,
                    item.ReasonCode is ResultReasons.NameConflict or ResultReasons.FamilyConflict
                        ? FileResultStatus.NameConflict : FileResultStatus.Failed, item.ReasonCode)).ToArray();
                Summary = ResultSummary.From(Results);
                SetProgress("Blocked");
                return;
            }

            FrozenExportBatch frozen;
            try
            {
                frozen = onUiThread ? await Task.Run(() => Session.FreezeForRun(selectedNx))
                    : Session.FreezeForRun(selectedNx);
            }
            catch (PreflightChangedException error)
            {
                Results = [PlanResult(error.Item, FileResultStatus.Failed, ResultReasons.TargetChanged, error.Message)];
                Summary = ResultSummary.From(Results);
                SetProgress("PreflightChanged");
                return;
            }

            var plan = frozen.Items;
            var readyItems = plan.Where(item => !item.IsSkipped).ToArray();
            UpdateProgress(state => state with { Export = new(readyItems.Length), Skipped = plan.Count - readyItems.Length });
            if (Progress.CancelRequested)
                return;

            // 全部目标 PDF 已存在且策略为跳过：不需要 NX，也不启动 Worker，
            // 更不创建运行目录——直接给出完整的跳过结果。
            if (readyItems.Length == 0)
            {
                Results = plan.Select(item => PlanResult(item, FileResultStatus.SkippedExisting, ResultReasons.SkippedExisting)).ToArray();
                Summary = ResultSummary.From(Results);
                SetProgress("AllSkipped");
                log.Write(ProgressText);
                return;
            }

            var workerExePath = services.WorkerExePath
                ?? Path.Combine(AppContext.BaseDirectory, selectedNx.Profile.WorkerRelativePath);
            if (!File.Exists(workerExePath))
            {
                SetProgress("WorkerMissing", selectedNx.Profile.Label, selectedNx.Profile.WorkerRelativePath);
                return;
            }

            // 运行目录承载 job.json、result.json 与本次运行日志。
            var runId = Guid.NewGuid().ToString("N");
            var runRoot = services.RunRootProvider?.Invoke()
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NxDrawingPdfExporter", "runs");
            var runDirectory = Path.Combine(runRoot, runId);
            Directory.CreateDirectory(runDirectory);
            CurrentRunDirectory = runDirectory;
            var runLog = new RunLog(runDirectory);

            var jobItems = new List<JobItem>();
            foreach (var item in readyItems)
            {
                var tempName = ".nxpdf-" + Guid.NewGuid().ToString("N") + ".tmp.pdf";
                jobItems.Add(new JobItem
                {
                    SourcePath = item.SourcePath,
                    FinalOutputPath = item.FinalOutputPath,
                    WorkerTempOutputPath = Path.Combine(Path.GetDirectoryName(item.FinalOutputPath)!, tempName)
                });
            }

            var request = new JobRequest
            {
                RunId = runId,
                Items = jobItems.ToArray(),
                OutputMode = frozen.Routing.Mode,
                UnifiedOutputDirectory = frozen.Routing.Mode == OutputMode.UnifiedDirectory ? frozen.Routing.UnifiedDirectory : null,
                ExistingPdfPolicy = frozen.Policy,
                ResultPath = Path.Combine(runDirectory, "result.json"),
                CancellationFlagPath = Path.Combine(runDirectory, "cancel.flag")
            };

            var jobPath = Path.Combine(runDirectory, "job.json");
            JobJsonSerializer.WriteToFile(request, jobPath);
            cancellationFlagPath = request.CancellationFlagPath;
            if (Progress.CancelRequested)
                return;
            runLog.Write($"运行开始：{jobItems.Count} 个候选文件。");
            SetProgress("WorkerStarting");
            var generation = Progress.Generation;
            UpdateProgress(state => state with { RunId = runId, Phase = RunPhase.Initializing });
            if (Progress.CancelRequested)
                return;
            var knownNxRoots = NxDiscovery.Installations.Select(i => i.RootDirectory).ToArray();
            await using var monitor = new RunProgressMonitor(runDirectory, runId, readyItems.Length,
                snapshot => AcceptExportProgress(snapshot, generation, request), issue =>
                {
                    if (issue is not null)
                    {
                        try { runLog.Write("Advisory progress unavailable: " + issue); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        { System.Diagnostics.Debug.WriteLine(error); }
                    }
                    UpdateProgress(state => state with { FeedbackUnavailable = issue is not null });
                });
            _ = monitor.StartAsync();

            DateTime launchedUtc = DateTime.UtcNow;
            WorkerLaunchResult launch;
            try
            {
                launch = await Task.Run(() => services.WorkerLauncher.Launch(new WorkerLaunchRequest
                {
                    Installation = frozen.Installation,
                    KnownNxRoots = knownNxRoots,
                    NxLauncherPath = launcherPathForRun,
                    WorkerExePath = workerExePath,
                    JobPath = jobPath,
                    RunDirectory = runDirectory
                }));
            }
            catch (Exception error)
            {
                runLog.Write("Worker 启动失败: " + SingleLine(error.Message));
                SetProgress("WorkerFailed");
                return;
            }
            finally
            {
                await monitor.StopAsync();
            }

            await monitor.ReadFinalAsync();
            UpdateProgress(state => state with
            {
                Phase = RunPhase.CheckingWorkerResult, CurrentItemIndex = null, CurrentSourcePath = null
            });
            if (launch.TimedOut)
            {
                runLog.Write("Worker 运行超时，已终止。");
                SetProgress("WorkerTimeout");
                return;
            }

            JobResult result;
            try
            {
                result = await Task.Run(() => JobJsonSerializer.ReadFromFile<JobResult>(request.ResultPath));
            }
            catch (Exception error)
            {
                runLog.Write("Worker 未写出有效的 result.json: " + SingleLine(error.Message));
                // 不可信结果：不发布任何文件，也不残留运行临时 PDF。
                CleanAllRunOwnedTemps(request, runLog);
                SetProgress("WorkerInvalid");
                return;
            }

            // 结果级校验：结果文件必须属于本次运行，否则整体拒绝发布。
            if (!WorkerResultValidator.IsTrusted(request, result, launchedUtc, DateTime.UtcNow))
            {
                runLog.Write("Worker 结果的任务身份、条目或完成时间无效，拒绝发布任何文件。");
                CleanAllRunOwnedTemps(request, runLog);
                Results = readyItems.Select(item => new FileResult
                {
                    SourcePath = item.SourcePath,
                    FinalOutputPath = item.FinalOutputPath,
                    Status = FileResultStatus.Failed,
                    ReasonCode = ResultReasons.WorkerResultInvalid,
                    Message = "Worker 结果文件与本次运行不匹配，不能作为成功依据。"
                }).ToArray();
                Summary = ResultSummary.From(Results);
                SetProgress("WorkerMismatch");
                return;
            }

            // 逐文件验证并事务性发布；只有通过校验的临时 PDF 才会成为正式输出。
            // 跳过项来自预检查，从未进入 Worker 任务。
            // 导出阶段的取消保留已经完成的条目。进入发布阶段后收到的新请求
            // 在当前安全发布完成后停止处理剩余候选。
            ConfirmExportCounts(result, request, runLog);
            var publisher = new SafeOutputPublisher(services.PdfInspector, services.FileReplacer, services.TargetStateReader);
            var workerResults = result.Files
                .GroupBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var fatal = result.FatalError;
            var finalResults = new List<FileResult>(plan.Count);
            var publicationItems = plan.Where(item => !item.IsSkipped &&
                workerResults.TryGetValue(item.SourcePath, out var file) && file.Status == FileResultStatus.Success).ToArray();
            UpdateProgress(state => state with { Phase = RunPhase.Publishing, PublicationReached = true,
                Publication = new(publicationItems.Length), FeedbackUnavailable = false });
            var publicationIndex = 0;
            for (var index = 0; index < plan.Count; index++)
            {
                var planItem = plan[index];
                if (planItem.IsSkipped)
                {
                    finalResults.Add(PlanResult(planItem, FileResultStatus.SkippedExisting, ResultReasons.SkippedExisting));
                    continue;
                }

                if (!workerResults.TryGetValue(planItem.SourcePath, out var workerResult))
                {
                    DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                    finalResults.Add(new FileResult
                    {
                        SourcePath = planItem.SourcePath, FinalOutputPath = planItem.FinalOutputPath,
                        Status = FileResultStatus.Failed, ReasonCode = ResultReasons.WorkerResultInvalid,
                        TechnicalDetails = fatal ?? "",
                        Message = fatal is null ? "Worker 未报告该文件的结果。" : "批处理在完成该文件前发生致命错误: " + fatal
                    });
                    continue;
                }
                if (workerResult.Status != FileResultStatus.Success)
                {
                    DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                    finalResults.Add(workerResult);
                    continue;
                }
                if (publicationCancellation)
                {
                    DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                    finalResults.Add(PlanResult(planItem, FileResultStatus.Cancelled, ResultReasons.Cancelled));
                    continue;
                }

                var activeIndex = publicationIndex++;
                UpdateProgress(state => state with
                {
                    CurrentItemIndex = activeIndex, CurrentSourcePath = publicationItems[activeIndex].SourcePath
                });
                var published = await Task.Run(() =>
                {
                    if (!Session.IsCurrent(planItem, out var details))
                    {
                        DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                        return PlanResult(planItem, FileResultStatus.Failed, ResultReasons.TargetChanged, details);
                    }
                    return PublishOne(request, workerResult, publisher, runLog, planItem);
                });
                finalResults.Add(published);
                var saved = published.Status is FileResultStatus.Success or FileResultStatus.Overwritten;
                UpdateProgress(state => state with
                {
                    CurrentItemIndex = null, CurrentSourcePath = null,
                    Publication = state.Publication with
                    {
                        Processed = state.Publication.Processed + 1,
                        Saved = state.Publication.Saved + (saved ? 1 : 0),
                        Failed = state.Publication.Failed + (saved ? 0 : 1),
                        Overwritten = state.Publication.Overwritten + (published.Status == FileResultStatus.Overwritten ? 1 : 0)
                    }
                });
            }

            Session.Invalidate();
            Results = finalResults;
            Summary = ResultSummary.From(finalResults);
            if (fatal is not null)
            {
                runLog.Write("批处理致命错误: " + fatal);
                SetProgress("Fatal");
            }
            else if (publicationCancellation)
            {
                SetProgress("PublishStopped");
            }
            else if (result.Cancelled)
            {
                SetProgress("WorkerCancelled");
            }
            else
            {
                SetProgress("Completed");
            }

            runLog.Write(Summary.ToChineseSummary());
        }

        /// <summary>删除本次运行自有的临时 PDF；未发布或未报告的条目不得残留临时文件。</summary>
        private void DeleteRunOwnedTemp(JobRequest request, string sourcePath, RunLog runLog)
        {
            var item = request.Items.FirstOrDefault(i => string.Equals(i.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
            if (item is null || !File.Exists(item.WorkerTempOutputPath))
            {
                return;
            }

            try
            {
                services.FileReplacer.DeleteFile(item.WorkerTempOutputPath);
            }
            catch (Exception error)
            {
                runLog.Write("清理运行临时 PDF 失败: " + SingleLine(error.Message));
            }
        }

        /// <summary>整体拒绝发布（结果不可信）时清理全部运行自有临时 PDF。</summary>
        private void CleanAllRunOwnedTemps(JobRequest request, RunLog runLog)
        {
            foreach (var item in request.Items)
            {
                DeleteRunOwnedTemp(request, item.SourcePath, runLog);
            }
        }

        private FileResult PublishOne(JobRequest request, FileResult fileResult, SafeOutputPublisher publisher, RunLog runLog, PlannedOutputItem planned)
        {
            if (fileResult.Status != FileResultStatus.Success)
            {
                // 非 Success 的条目不存在可发布的临时 PDF；若 Worker 因异常
                // 残留了本次运行自有的临时文件，一律清理，绝不发布未校验内容。
                DeleteRunOwnedTemp(request, fileResult.SourcePath, runLog);
                return fileResult;
            }

            var item = request.Items.FirstOrDefault(i => string.Equals(i.SourcePath, fileResult.SourcePath, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return Failed(fileResult, "结果文件与任务条目不匹配。");
            }

            var expectedPages = fileResult.ExportedSheets.Length;
            if (expectedPages < 1)
            {
                DeleteRunOwnedTemp(request, fileResult.SourcePath, runLog);
                return Failed(fileResult, "Worker 报告成功但没有导出任何图纸页。");
            }

            try
            {
                var outcome = publisher.Publish(new PublicationRequest
                {
                    TempPdfPath = item.WorkerTempOutputPath,
                    FinalOutputPath = item.FinalOutputPath,
                    ExpectedPageCount = expectedPages,
                    Intent = planned.PublishIntent,
                    ExpectedTarget = planned.ExpectedTarget
                });

                var published = Done(fileResult, outcome.Status switch
                {
                    PublicationOutcomeStatus.Published => FileResultStatus.Success,
                    PublicationOutcomeStatus.Replaced => FileResultStatus.Overwritten,
                    _ => FileResultStatus.Failed
                }, outcome.Message);
                published.ReasonCode = outcome.ReasonCode;
                published.TechnicalDetails = outcome.TechnicalDetails;
                return published;
            }
            catch (Exception error)
            {
                runLog.Write("发布异常: " + SingleLine(error.Message));
                return Failed(fileResult, "发布 PDF 失败: " + SingleLine(error.Message));
            }
        }

        private static FileResult PlanResult(PlannedOutputItem item, FileResultStatus status, string reason, string? details = null) => new()
        {
            SourcePath = item.SourcePath,
            FinalOutputPath = item.FinalOutputPath,
            Status = status,
            ReasonCode = reason,
            ReasonArguments = item.ReasonArguments.ToArray(),
            TechnicalDetails = details ?? item.TechnicalDetails
        };

        private static FileResult Done(FileResult source, FileResultStatus status, string message)
        {
            return new FileResult
            {
                SourcePath = source.SourcePath,
                FinalOutputPath = source.FinalOutputPath,
                Status = status,
                Message = message,
                ExportedSheets = source.ExportedSheets,
                SkippedSheets = source.SkippedSheets,
                ElapsedMilliseconds = source.ElapsedMilliseconds
            };
        }

        private static FileResult Failed(FileResult source, string message)
        {
            var result = Done(source, FileResultStatus.Failed, message);
            result.ReasonCode = ResultReasons.PublicationFailed;
            result.TechnicalDetails = message;
            return result;
        }

        private (IReadOnlyList<string> Sources, string? Error) DiscoverSources(
            GuiInputMode mode, string folder, bool includeSubfolders, IReadOnlyList<string> manualPaths)
        {
            if (mode == GuiInputMode.ScanFolder && string.IsNullOrWhiteSpace(folder))
                return (Array.Empty<string>(), null);
            try
            {
                var sources = mode == GuiInputMode.ScanFolder
                    ? services.Discovery.ScanFolder(folder, includeSubfolders)
                    : services.Discovery.NormalizeManualSelection(manualPaths);
                return (sources, null);
            }
            catch (Exception error)
            {
                return (Array.Empty<string>(), error.Message);
            }
        }

        private static string SingleLine(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "未知错误"
                : message.Replace("\r", " ").Replace("\n", " ").Trim();
        }

        private string FormatNxStatus(NxInstallationDiscoveryResult discovery, NxSettingsLoadStatus settingsStatus)
        {
            if (discovery.SelectedInstallation is { } nx)
                return Text.Get("NxReady", nx.Profile.Label, nx.DetectedVersion, nx.UfVersion, nx.RootDirectory)
                    + (nx.Profile == NxRuntimeProfile.Nx2506 ? Text.Get("NxLegacy") : "");
            if (discovery.RequiresSelection)
                return Text.Get("NxPick");
            var settings = settingsStatus is NxSettingsLoadStatus.InvalidJson or NxSettingsLoadStatus.UnsupportedSchema
                ? Text.Get("NxSettingsInvalid") + " " : "";
            return settings + Text.Get("NxUnavailable") + " " + FormatNxIssues(discovery.Issues) + " " + Text.Get("NxBrowseHint");
        }

        private string FormatNxIssues(IReadOnlyList<NxDetectionIssue> issues) => string.Join(" ",
            issues.Select(issue => Text.Language == "en" ? Text.Get("NxIssue." + issue.Code) : issue.UserMessage).Distinct());

        private static bool SameRuntime(NxInstallation selected, NxInstallation? current) => current is not null
            && selected.Profile == current.Profile
            && string.Equals(selected.RootDirectory, current.RootDirectory, StringComparison.OrdinalIgnoreCase)
            && string.Equals(selected.LauncherPath, current.LauncherPath, StringComparison.OrdinalIgnoreCase)
            && selected.DetectedVersion == current.DetectedVersion && selected.UfVersion == current.UfVersion
            && selected.UtilitiesVersion == current.UtilitiesVersion;
    }
}
