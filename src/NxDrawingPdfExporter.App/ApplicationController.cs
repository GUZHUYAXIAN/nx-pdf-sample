using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Logging;
using NxDrawingPdfExporter.App.Runtime;
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

        public IOutputPlanner OutputPlanner { get; init; } = new OutputPlanner(new PhysicalTargetProbe());

        public IWorkerProcessLauncher WorkerLauncher { get; init; } = new WorkerProcessLauncher();

        public IPdfInspector PdfInspector { get; init; } = new Pdf.PdfSharpInspector();

        public IFileReplacer FileReplacer { get; init; } = new FileReplacer();

        /// <summary>运行目录根；默认 %LOCALAPPDATA%\NxDrawingPdfExporter\runs。</summary>
        public Func<string>? RunRootProvider { get; init; }

        /// <summary>Worker 程序路径；默认相对程序目录的 worker\NxDrawingPdfExporter.Worker.exe。</summary>
        public string? WorkerExePath { get; init; }

        /// <summary>NX 检测替换点；默认对已验证本机路径做 fail-closed 检测。</summary>
        public Func<NxInstallationDetection>? NxDetectionOverride { get; set; }
    }

    /// <summary>
    /// 无 UI 依赖的应用控制器：模式状态、预检查、运行编排、结果发布与取消。
    /// WinForms 窗体只是它的薄绑定层。
    /// </summary>
    public sealed class ApplicationController
    {
        private readonly ApplicationServices services;
        private readonly ILogSink log;
        private readonly NxInstallationDetector detector = new();
        private string? cancellationFlagPath;

        public ApplicationController(ApplicationServices services, ILogSink log)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        // ---- 用户可选状态（默认值来自批准的设计） ----
        public GuiInputMode InputMode { get; set; } = GuiInputMode.ScanFolder;

        public string ScanFolderPath { get; set; } = "";

        public bool IncludeSubfolders { get; set; }

        public IReadOnlyList<string> ManualPaths { get; set; } = Array.Empty<string>();

        public OutputMode OutputMode { get; set; } = OutputMode.BesideSource;

        public string UnifiedOutputDirectory { get; set; } = "";

        public ExistingPdfPolicy ExistingPdfPolicy { get; set; } = ExistingPdfPolicy.Skip;

        // ---- 运行时状态 ----
        public NxInstallationDetection NxStatus { get; private set; } = new();

        public bool IsBusy { get; private set; }

        public string ProgressText { get; private set; } = "就绪。";

        public IReadOnlyList<FileResult> Results { get; private set; } = Array.Empty<FileResult>();

        public ResultSummary? Summary { get; private set; }

        public string SummaryText => Summary?.ToChineseSummary() ?? "尚未运行。";

        public string? CurrentRunDirectory { get; private set; }

        public event Action? StateChanged;

        private void Raise() => StateChanged?.Invoke();

        public void RefreshNxStatus()
        {
            NxStatus = services.NxDetectionOverride?.Invoke() ?? detector.Detect();
            log.Write("NX 检测: " + (NxStatus.IsSupported
                ? "受支持版本 " + NxStatus.DetectedVersion
                : NxStatus.Message));
            Raise();
        }

        /// <summary>输出预检查列表，供 GUI 预检查网格显示；不执行任何导出。</summary>
        public IReadOnlyList<OutputPlanItem> Preflight()
        {
            var sources = CollectSources();
            if (sources.Count == 0)
            {
                return Array.Empty<OutputPlanItem>();
            }

            return services.OutputPlanner.Plan(
                sources,
                OutputMode,
                OutputMode == OutputMode.UnifiedDirectory ? UnifiedOutputDirectory : null,
                ExistingPdfPolicy);
        }

        public async Task RunAsync()
        {
            if (IsBusy)
            {
                throw new InvalidOperationException("已有任务正在运行。");
            }

            IsBusy = true;
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
            ProgressText = "已请求取消，将在当前文件完成后停止。";
            Raise();
        }

        private async Task RunCore()
        {
            RefreshNxStatus();
            if (!NxStatus.IsSupported)
            {
                ProgressText = NxStatus.Message;
                return;
            }

            var sources = CollectSources();
            if (sources.Count == 0)
            {
                ProgressText = "未发现任何候选 PRT 文件。";
                return;
            }

            var plan = services.OutputPlanner.Plan(
                sources,
                OutputMode,
                OutputMode == OutputMode.UnifiedDirectory ? UnifiedOutputDirectory : null,
                ExistingPdfPolicy);
            var conflicts = plan.Where(p => p.Status == OutputPlanStatus.NameConflict).ToArray();
            if (conflicts.Length > 0)
            {
                log.Write($"预检查发现 {conflicts.Length} 个输出名称冲突，已阻止执行。");
                Results = conflicts.Select(p => new FileResult
                {
                    SourcePath = p.SourcePath,
                    FinalOutputPath = p.FinalOutputPath,
                    Status = FileResultStatus.NameConflict,
                    Message = "输出名称冲突，该项未执行。"
                }).ToArray();
                Summary = ResultSummary.From(Results);
                ProgressText = "存在输出名称冲突，已阻止执行。";
                return;
            }

            var workerExePath = services.WorkerExePath
                ?? Path.Combine(AppContext.BaseDirectory, "worker", "NxDrawingPdfExporter.Worker.exe");
            if (!File.Exists(workerExePath))
            {
                ProgressText = "缺少 Worker 组件（worker\\NxDrawingPdfExporter.Worker.exe）。";
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
            foreach (var item in plan.Where(p => p.Status == OutputPlanStatus.Ready))
            {
                var tempName = "." + Path.GetFileName(item.FinalOutputPath) + "." + runId + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp.pdf";
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
                OutputMode = OutputMode,
                UnifiedOutputDirectory = OutputMode == OutputMode.UnifiedDirectory ? UnifiedOutputDirectory : null,
                ExistingPdfPolicy = ExistingPdfPolicy,
                ResultPath = Path.Combine(runDirectory, "result.json"),
                CancellationFlagPath = Path.Combine(runDirectory, "cancel.flag")
            };

            var jobPath = Path.Combine(runDirectory, "job.json");
            JobJsonSerializer.WriteToFile(request, jobPath);
            cancellationFlagPath = request.CancellationFlagPath;
            runLog.Write($"运行开始：{jobItems.Count} 个候选文件。");
            ProgressText = "正在启动 NX Worker…";
            Raise();

            WorkerLaunchResult launch;
            try
            {
                launch = await Task.Run(() => services.WorkerLauncher.Launch(new WorkerLaunchRequest
                {
                    NxLauncherPath = NxStatus.LauncherPath,
                    WorkerExePath = workerExePath,
                    JobPath = jobPath,
                    RunDirectory = runDirectory
                }));
            }
            catch (Exception error)
            {
                runLog.Write("Worker 启动失败: " + SingleLine(error.Message));
                ProgressText = "Worker 启动失败: " + SingleLine(error.Message);
                return;
            }

            if (launch.TimedOut)
            {
                runLog.Write("Worker 运行超时，已终止。");
                ProgressText = "Worker 运行超时，已终止；详见运行日志。";
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
                ProgressText = "Worker 未写出有效结果文件，本次运行不能视为成功。";
                return;
            }

            // 逐文件验证并事务性发布；只有通过校验的临时 PDF 才会成为正式输出。
            // 跳过项来自预检查，从未进入 Worker 任务。
            var publisher = new SafeOutputPublisher(services.PdfInspector, services.FileReplacer);
            var workerResults = result.Files
                .GroupBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var finalResults = new List<FileResult>(plan.Count);
            for (var index = 0; index < plan.Count; index++)
            {
                var planItem = plan[index];
                if (planItem.Status == OutputPlanStatus.SkippedExisting)
                {
                    finalResults.Add(new FileResult
                    {
                        SourcePath = planItem.SourcePath,
                        FinalOutputPath = planItem.FinalOutputPath,
                        Status = FileResultStatus.SkippedExisting,
                        Message = "已存在，已跳过。"
                    });
                    continue;
                }

                ProgressText = $"正在验证并发布 {finalResults.Count + 1}/{plan.Count}…";
                Raise();
                if (!workerResults.TryGetValue(planItem.SourcePath, out var workerResult))
                {
                    finalResults.Add(new FileResult
                    {
                        SourcePath = planItem.SourcePath,
                        FinalOutputPath = planItem.FinalOutputPath,
                        Status = FileResultStatus.Failed,
                        Message = "Worker 未报告该文件的结果。"
                    });
                    continue;
                }

                finalResults.Add(PublishOne(request, workerResult, publisher, runLog));
            }

            Results = finalResults;
            Summary = ResultSummary.From(finalResults);
            ProgressText = "运行完成。";
            runLog.Write(Summary.ToChineseSummary());
        }

        private FileResult PublishOne(JobRequest request, FileResult fileResult, SafeOutputPublisher publisher, RunLog runLog)
        {
            if (fileResult.Status != FileResultStatus.Success)
            {
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
                    ExpectedPageCount = expectedPages
                });

                return outcome.Status switch
                {
                    PublicationOutcomeStatus.Published => Done(fileResult, FileResultStatus.Success, outcome.Message),
                    PublicationOutcomeStatus.Replaced => Done(fileResult, FileResultStatus.Overwritten, outcome.Message),
                    _ => Failed(fileResult, outcome.Message)
                };
            }
            catch (Exception error)
            {
                runLog.Write("发布异常: " + SingleLine(error.Message));
                return Failed(fileResult, "发布 PDF 失败: " + SingleLine(error.Message));
            }
        }

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
            return Done(source, FileResultStatus.Failed, message);
        }

        private IReadOnlyList<string> CollectSources()
        {
            try
            {
                return InputMode == GuiInputMode.ScanFolder
                    ? services.Discovery.ScanFolder(ScanFolderPath, IncludeSubfolders)
                    : services.Discovery.NormalizeManualSelection(ManualPaths);
            }
            catch (Exception error)
            {
                log.Write("输入收集失败: " + SingleLine(error.Message));
                ProgressText = "输入收集失败: " + SingleLine(error.Message);
                return Array.Empty<string>();
            }
        }

        private static string SingleLine(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "未知错误"
                : message.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
