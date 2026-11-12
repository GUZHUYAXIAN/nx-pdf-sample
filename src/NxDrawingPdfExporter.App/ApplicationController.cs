using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Session;
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
    public sealed class ApplicationController
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
            Session.Update(CollectSources(),
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
            Raise();
            try
            {
                await RunCore();
            }
            finally
            {
                IsBusy = false;
                cancellationFlagPath = null;
                Raise();
            }
        }

        /// <summary>请求取消：创建取消标志文件；绝不终止正在进行的原子操作。</summary>
        public void Cancel()
        {
            var path = cancellationFlagPath;
            if (path is null || File.Exists(path))
            {
                return;
            }

            File.WriteAllText(path, "cancel");
            log.Write("用户请求取消，已写入取消标志。");
            SetProgress("Stopping");
            Raise();
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

            UpdatePreflightInputs();
            if (Session.Inputs.Count == 0)
            {
                SetProgress(InputTechnicalDetails is null ? "NoInput" : "InputFailed");
                return;
            }

            var reviewed = explicitlyReviewed ? Session.CurrentPlan! : Session.Refresh();
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
                frozen = Session.FreezeForRun(selectedNx);
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
            runLog.Write($"运行开始：{jobItems.Count} 个候选文件。");
            SetProgress("WorkerStarting");
            Raise();

            DateTime launchedUtc = DateTime.UtcNow;
            WorkerLaunchResult launch;
            try
            {
                launch = await Task.Run(() => services.WorkerLauncher.Launch(new WorkerLaunchRequest
                {
                    Installation = frozen.Installation,
                    KnownNxRoots = NxDiscovery.Installations.Select(i => i.RootDirectory).ToArray(),
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

            if (launch.TimedOut)
            {
                runLog.Write("Worker 运行超时，已终止。");
                SetProgress("WorkerTimeout");
                return;
            }

            JobResult result;
            try
            {
                result = JobJsonSerializer.ReadFromFile<JobResult>(request.ResultPath);
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
            // 取消语义：Worker 退出时标志若已存在，说明 Worker 已按文件边界
            // 处理过取消——已完成的条目必须照常发布，不得重复取消；只有
            // Worker 退出后新出现的标志才属于发布阶段的新取消请求。
            var flagPresentAtWorkerExit = IsCancellationRequested();
            var publisher = new SafeOutputPublisher(services.PdfInspector, services.FileReplacer, services.TargetStateReader);
            var workerResults = result.Files
                .GroupBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var fatal = result.FatalError;
            var finalResults = new List<FileResult>(plan.Count);
            var cancellationRequested = false;
            for (var index = 0; index < plan.Count; index++)
            {
                var planItem = plan[index];
                if (planItem.IsSkipped)
                {
                    finalResults.Add(PlanResult(planItem, FileResultStatus.SkippedExisting, ResultReasons.SkippedExisting));
                    continue;
                }

                if (!cancellationRequested && !flagPresentAtWorkerExit && IsCancellationRequested())
                {
                    cancellationRequested = true;
                    runLog.Write("发布阶段检测到取消请求：完成当前文件后不再发布后续文件。");
                    SetProgress("PublishStopping");
                    Raise();
                }

                if (cancellationRequested)
                {
                    DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                    finalResults.Add(new FileResult
                    {
                        SourcePath = planItem.SourcePath,
                        FinalOutputPath = planItem.FinalOutputPath,
                        Status = FileResultStatus.Cancelled,
                        ReasonCode = ResultReasons.Cancelled,
                        Message = "已请求取消，该文件未被发布。"
                    });
                    continue;
                }

                SetProgress("Publishing", finalResults.Count + 1, plan.Count);
                Raise();
                if (!workerResults.TryGetValue(planItem.SourcePath, out var workerResult))
                {
                    DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                    finalResults.Add(new FileResult
                    {
                        SourcePath = planItem.SourcePath,
                        FinalOutputPath = planItem.FinalOutputPath,
                        Status = FileResultStatus.Failed,
                        ReasonCode = ResultReasons.WorkerResultInvalid,
                        TechnicalDetails = fatal ?? "",
                        Message = fatal is null
                            ? "Worker 未报告该文件的结果。"
                            : "批处理在完成该文件前发生致命错误: " + fatal
                    });
                    continue;
                }

                if (workerResult.Status == FileResultStatus.Success && !Session.IsCurrent(planItem, out var details))
                {
                    DeleteRunOwnedTemp(request, planItem.SourcePath, runLog);
                    finalResults.Add(PlanResult(planItem, FileResultStatus.Failed, ResultReasons.TargetChanged, details));
                    continue;
                }
                finalResults.Add(PublishOne(request, workerResult, publisher, runLog, planItem));
            }

            Session.Invalidate();
            Results = finalResults;
            Summary = ResultSummary.From(finalResults);
            if (fatal is not null)
            {
                runLog.Write("批处理致命错误: " + fatal);
                SetProgress("Fatal");
            }
            else if (cancellationRequested)
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

        /// <summary>取消标志是否已被置位（GUI 写入，Worker 与发布阶段都会观察）。</summary>
        private bool IsCancellationRequested()
        {
            var path = cancellationFlagPath;
            return path is not null && File.Exists(path);
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

        private IReadOnlyList<string> CollectSources()
        {
            InputTechnicalDetails = null;
            if (InputMode == GuiInputMode.ScanFolder && string.IsNullOrWhiteSpace(ScanFolderPath))
                return Array.Empty<string>();
            try
            {
                return InputMode == GuiInputMode.ScanFolder
                    ? services.Discovery.ScanFolder(ScanFolderPath, IncludeSubfolders)
                    : services.Discovery.NormalizeManualSelection(ManualPaths);
            }
            catch (Exception error)
            {
                InputTechnicalDetails = error.Message;
                log.Write("输入收集失败: " + SingleLine(error.Message));
                SetProgress("InputFailed");
                return Array.Empty<string>();
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
