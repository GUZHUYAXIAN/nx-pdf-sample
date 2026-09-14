using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NxDrawingPdfExporter.App.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Logging;
using NxDrawingPdfExporter.App.Pdf;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Jobs;
using NxDrawingPdfExporter.Core.Output;
using NxDrawingPdfExporter.Core.Pdf;

namespace NxDrawingPdfExporter.App.Tests
{
    [TestClass]
    public sealed class ApplicationControllerTests
    {
        private string scratch = "";
        private FakeWorkerLauncher launcher = null!;

        [TestInitialize]
        public void Initialize()
        {
            scratch = Path.Combine(Path.GetTempPath(), "nxpdf-controller-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(scratch, "in"));
            Directory.CreateDirectory(Path.Combine(scratch, "out"));
            File.WriteAllText(Path.Combine(scratch, "in", "drawing1.prt"), "prt");
            File.WriteAllText(Path.Combine(scratch, "in", "drawing2.prt"), "prt");
            launcher = new FakeWorkerLauncher();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }

        private ApplicationServices NewServices(IPdfInspector? inspector = null)
        {
            var workerExe = Path.Combine(scratch, "worker.exe");
            File.WriteAllText(workerExe, "worker");
            return new ApplicationServices
            {
                WorkerLauncher = launcher,
                RunRootProvider = () => Path.Combine(scratch, "runs"),
                WorkerExePath = workerExe,
                PdfInspector = inspector ?? new StubInspector(pageCount: 1),
                NxInstallationDiscovery = new StubNxDiscovery(ReadyNxInstallation()),
                NxSettings = new StubNxSettings()
            };
        }

        private ApplicationController NewController(ApplicationServices? services = null)
        {
            var controller = new ApplicationController(services ?? NewServices(), new NullSink());
            controller.RefreshNxStatusAsync().GetAwaiter().GetResult();
            return controller;
        }

        private static NxInstallation ReadyNxInstallation() => new(
            @"E:\Test-NX",
            @"E:\Test-NX\UGII\run_managed.exe",
            @"E:\Test-NX\UGII\managed\NXOpen.dll",
            "10.0.0.24",
            new[] { NxCandidateSource.Registry });

        private static FileResult WorkerSuccess(JobItem item, string[] exportedSheets)
        {
            return new FileResult
            {
                SourcePath = item.SourcePath,
                FinalOutputPath = item.FinalOutputPath,
                Status = FileResultStatus.Success,
                Message = "临时 PDF 已生成，等待验证发布。",
                ExportedSheets = exportedSheets
            };
        }

        [TestMethod]
        public void Defaults_AreApprovedModes()
        {
            var controller = NewController();

            Assert.AreEqual(GuiInputMode.ScanFolder, controller.InputMode);
            Assert.IsFalse(controller.IncludeSubfolders);
            Assert.AreEqual(OutputMode.BesideSource, controller.OutputMode);
            Assert.AreEqual(ExistingPdfPolicy.Skip, controller.ExistingPdfPolicy);
            Assert.IsFalse(controller.IsBusy);
        }

        [TestMethod]
        public async Task Run_MissingNxInstallation_IsRefused()
        {
            var services = NewServices();
            ((StubNxDiscovery)services.NxInstallationDiscovery).Result = new NxInstallationDiscoveryResult(
                Array.Empty<NxInstallation>(),
                Array.Empty<NxDetectionIssue>(),
                null);
            var controller = NewController(services);
            controller.ScanFolderPath = Path.Combine(scratch, "in");

            await controller.RunAsync();

            Assert.IsFalse(launcher.LaunchCalls.Any());
            Assert.IsFalse(controller.IsBusy);
        }

        [TestMethod]
        public async Task Run_UsesSelectedCustomNxLauncher_NotV1DefaultPath()
        {
            var services = NewServices();
            var customInstallation = new NxInstallation(
                @"E:\Apps\NX10",
                @"E:\Apps\NX10\UGII\run_managed.exe",
                @"E:\Apps\NX10\UGII\managed\NXOpen.dll",
                "10.0.0.24",
                new[] { NxCandidateSource.Registry });
            ((StubNxDiscovery)services.NxInstallationDiscovery).Result = new NxInstallationDiscoveryResult(
                new[] { customInstallation },
                Array.Empty<NxDetectionIssue>(),
                customInstallation);
            var controller = NewController(services);
            controller.ScanFolderPath = Path.Combine(scratch, "in");

            await controller.RunAsync();

            Assert.AreEqual(
                customInstallation.LauncherPath,
                launcher.LaunchCalls.Single().NxLauncherPath);
        }

        [TestMethod]
        public async Task Run_ScanFolder_ExportsValidatedPdfsAndSummarizes()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");

            await controller.RunAsync();

            Assert.IsFalse(controller.IsBusy);
            Assert.HasCount(2, controller.Results);
            Assert.IsTrue(controller.Results.All(r => r.Status == FileResultStatus.Success));
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing2.pdf")));
            Assert.IsFalse(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf").Any());
            StringAssert.Contains(controller.SummaryText, "2");
            Assert.HasCount(1, launcher.LaunchCalls);
        }

        [TestMethod]
        public async Task Run_ManualSelection_PreservesUserOrder()
        {
            var controller = NewController();
            controller.InputMode = GuiInputMode.ManualSelection;
            controller.ManualPaths = new[]
            {
                Path.Combine(scratch, "in", "drawing2.prt"),
                Path.Combine(scratch, "in", "drawing1.prt")
            };

            await controller.RunAsync();

            Assert.HasCount(2, controller.Results);
            StringAssert.Contains(controller.Results[0].SourcePath, "drawing2");
            StringAssert.Contains(controller.Results[1].SourcePath, "drawing1");
        }

        [TestMethod]
        public async Task Run_SkipPolicyWithExistingPdf_ReportsSkippedWithoutLaunch()
        {
            Directory.CreateDirectory(Path.Combine(scratch, "out"));
            File.WriteAllText(Path.Combine(scratch, "in", "drawing1.pdf"), "existing");
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");

            await controller.RunAsync();

            Assert.AreEqual(FileResultStatus.SkippedExisting, controller.Results[0].Status);
            Assert.AreEqual("existing", File.ReadAllText(Path.Combine(scratch, "in", "drawing1.pdf")));
            // The worker is still launched for the remaining item only.
            var job = JobJsonSerializer.ReadFromFile<JobRequest>(launcher.LaunchCalls[0].JobPath);
            Assert.HasCount(1, job.Items);
            StringAssert.Contains(job.Items[0].SourcePath, "drawing2");
        }

        [TestMethod]
        public async Task Run_OverwritePolicy_ReplacesExistingPdfOnlyAfterValidation()
        {
            Directory.CreateDirectory(Path.Combine(scratch, "out"));
            File.WriteAllText(Path.Combine(scratch, "in", "drawing1.pdf"), "old bytes");
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            controller.ExistingPdfPolicy = ExistingPdfPolicy.Overwrite;

            await controller.RunAsync();

            Assert.HasCount(2, controller.Results);
            Assert.AreEqual(FileResultStatus.Overwritten, controller.Results[0].Status);
            StringAssert.Contains(File.ReadAllText(Path.Combine(scratch, "in", "drawing1.pdf")), "%PDF-");
        }

        [TestMethod]
        public async Task Run_UnifiedNameConflict_IsBlockedWithoutLaunch()
        {
            Directory.CreateDirectory(Path.Combine(scratch, "in2"));
            File.WriteAllText(Path.Combine(scratch, "in2", "drawing1.prt"), "prt");
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            controller.InputMode = GuiInputMode.ManualSelection;
            controller.ManualPaths = new[]
            {
                Path.Combine(scratch, "in", "drawing1.prt"),
                Path.Combine(scratch, "in2", "drawing1.prt")
            };
            controller.OutputMode = OutputMode.UnifiedDirectory;
            controller.UnifiedOutputDirectory = Path.Combine(scratch, "out");

            await controller.RunAsync();

            Assert.IsFalse(launcher.LaunchCalls.Any());
            StringAssert.Contains(controller.SummaryText, "冲突");
        }

        [TestMethod]
        public async Task Run_WorkerFailure_ContinuesAndMarksFailed()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.FailForSourceContains = "drawing1";

            await controller.RunAsync();

            Assert.AreEqual(FileResultStatus.Failed, controller.Results[0].Status);
            Assert.AreEqual(FileResultStatus.Success, controller.Results[1].Status);
            Assert.IsTrue(controller.Summary!.HasFailures);
        }

        [TestMethod]
        public async Task Run_WorkerReportsPureModel_IsMappedWithoutPublication()
        {
            var controller = NewController();
            controller.InputMode = GuiInputMode.ManualSelection;
            controller.ManualPaths = new[] { Path.Combine(scratch, "in", "drawing1.prt") };
            launcher.ReportStatusForAll = FileResultStatus.PureModel;

            await controller.RunAsync();

            Assert.AreEqual(FileResultStatus.PureModel, controller.Results[0].Status);
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
        }

        [TestMethod]
        public async Task Run_InvalidWorkerPdf_FailsInsteadOfPublishing()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.WriteGarbageTempPdfs = true;

            await controller.RunAsync();

            Assert.IsTrue(controller.Results.All(r => r.Status == FileResultStatus.Failed));
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
        }

        [TestMethod]
        public async Task Cancel_DuringRun_WritesCancellationFlag()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.HoldUntilReleased = true;
            var run = controller.RunAsync();

            SpinWait.SpinUntil(() => controller.CurrentRunDirectory != null, 5000);
            // 等到 Worker（fake）已完成全部条目并写出结果、正被 hold 阻塞时
            // 再取消：模拟"取消请求到达时 Worker 已经做完"的时序。
            var resultPath = Path.Combine(controller.CurrentRunDirectory!, "result.json");
            Assert.IsTrue(SpinWait.SpinUntil(() => File.Exists(resultPath), 10000), "结果文件未在时限内写出。");
            controller.Cancel();

            Assert.IsTrue(File.Exists(Path.Combine(controller.CurrentRunDirectory!, "cancel.flag")));
            launcher.WaitGate.TrySetResult(true);
            await run;

            // Worker 在观察到标志前已完成全部条目：已完成的文件必须照常
            // 发布，不得被发布阶段的取消分支误杀（二审 P1）。
            Assert.IsFalse(controller.IsBusy);
            Assert.HasCount(2, controller.Results);
            Assert.IsTrue(controller.Results.All(r => r.Status == FileResultStatus.Success));
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing2.pdf")));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
        }

        [TestMethod]
        public async Task Cancel_DuringWorkerRun_PublishesCompletedFilesAndKeepsWorkerCancelledRest()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.ModelWorkerSideCancellation = true;
            var run = controller.RunAsync();

            // 用户在条目 1 完成、条目 2 未开始时点击取消（真实 Worker 随后
            // 在文件边界取消剩余条目并返回 [Success, Cancelled]）。
            Assert.IsTrue(launcher.FirstItemDone.Task.Wait(15000), "条目 1 未在时限内完成。");
            controller.Cancel();
            launcher.CancelGate.TrySetResult(true);
            await run;

            // 核心（二审 P1）：Worker 已完成的条目 1 必须照常发布，
            // 不得因取消标志仍存在而被删除或改判。
            Assert.AreEqual(FileResultStatus.Success, controller.Results[0].Status);
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.AreEqual(FileResultStatus.Cancelled, controller.Results[1].Status);
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing2.pdf")));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
            Assert.AreEqual(1, controller.Summary!.Cancelled);
            StringAssert.Contains(controller.ProgressText, "取消");
        }

        [TestMethod]
        public async Task Run_SecondRunFailsBeforeLaunch_DoesNotRetainFirstRunSuccessState()
        {
            var services = NewServices();
            var controller = NewController(services);
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            await controller.RunAsync();
            Assert.HasCount(2, controller.Results);
            Assert.IsNotNull(controller.Summary);
            Assert.IsNotNull(controller.CurrentRunDirectory);

            ((StubNxDiscovery)services.NxInstallationDiscovery).Result = new NxInstallationDiscoveryResult(
                Array.Empty<NxInstallation>(),
                Array.Empty<NxDetectionIssue>(),
                null);
            await controller.RefreshNxStatusAsync();
            await controller.RunAsync();

            Assert.HasCount(0, controller.Results);
            Assert.IsNull(controller.Summary);
            Assert.IsNull(controller.CurrentRunDirectory);
            Assert.IsFalse(controller.IsBusy);
            StringAssert.Contains(controller.ProgressText, "NX");
        }

        [TestMethod]
        public async Task Run_SecondRunInvalidResultJson_KeepsOnlyCurrentRunState()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            await controller.RunAsync();
            var firstRunDirectory = controller.CurrentRunDirectory!;
            Assert.HasCount(2, controller.Results);
            // 第二轮不能因“目标已存在”走跳过路径；清掉首轮产物使其真正进入 Worker。
            foreach (var produced in Directory.GetFiles(Path.Combine(scratch, "in"), "*.pdf"))
            {
                File.Delete(produced);
            }

            launcher.WriteInvalidResultJson = true;
            await controller.RunAsync();

            Assert.HasCount(0, controller.Results);
            Assert.IsNull(controller.Summary);
            Assert.IsNotNull(controller.CurrentRunDirectory);
            Assert.AreNotEqual(firstRunDirectory, controller.CurrentRunDirectory);
            StringAssert.Contains(controller.ProgressText, "结果文件");
        }

        [TestMethod]
        public async Task Run_AllTargetsExistWithSkipPolicy_NeverLaunchesWorkerOrCreatesRunDirectory()
        {
            File.WriteAllText(Path.Combine(scratch, "in", "drawing1.pdf"), "existing-1");
            File.WriteAllText(Path.Combine(scratch, "in", "drawing2.pdf"), "existing-2");
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");

            await controller.RunAsync();

            Assert.HasCount(2, controller.Results);
            Assert.IsTrue(controller.Results.All(r => r.Status == FileResultStatus.SkippedExisting));
            Assert.IsEmpty(launcher.LaunchCalls);
            Assert.IsNull(controller.CurrentRunDirectory);
            Assert.IsFalse(Directory.Exists(Path.Combine(scratch, "runs")));
            Assert.AreEqual("existing-1", File.ReadAllText(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.AreEqual("existing-2", File.ReadAllText(Path.Combine(scratch, "in", "drawing2.pdf")));
            StringAssert.Contains(controller.SummaryText, "已有跳过 2");
        }

        [TestMethod]
        public async Task Cancel_DuringPublication_PublishesCurrentFileThenCancelsRest()
        {
            var firstInspectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var services = NewServices(new StubInspector(pageCount: 1)
            {
                OnInspect = () =>
                {
                    firstInspectStarted.TrySetResult(true);
                    releaseGate.Task.Wait(15000);
                }
            });
            var controller = NewController(services);
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            var run = controller.RunAsync();

            Assert.IsTrue(firstInspectStarted.Task.Wait(15000), "第一个文件的发布校验未开始。");
            controller.Cancel();
            Assert.IsTrue(releaseGate.TrySetResult(true));
            await run;

            Assert.AreEqual(FileResultStatus.Success, controller.Results[0].Status);
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.AreEqual(FileResultStatus.Cancelled, controller.Results[1].Status);
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing2.pdf")));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
            Assert.AreEqual(1, controller.Summary!.Cancelled);
            Assert.IsFalse(controller.IsBusy);
            StringAssert.Contains(controller.ProgressText, "取消");
        }

        [TestMethod]
        public async Task Run_ResultFromDifferentRunId_AllItemsFailWithoutPublication()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.ResultRunIdOverride = "run-from-another-life";

            await controller.RunAsync();

            Assert.HasCount(2, controller.Results);
            Assert.IsTrue(controller.Results.All(r => r.Status == FileResultStatus.Failed));
            Assert.IsTrue(controller.Summary!.HasFailures);
            StringAssert.Contains(controller.ProgressText, "不匹配");
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing2.pdf")));
            // 不可信结果不得残留运行临时 PDF（二审 P2）。
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
        }

        [TestMethod]
        public async Task Run_InvalidResultJson_CleansRunOwnedTemps()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.WriteInvalidResultJson = true;

            await controller.RunAsync();

            Assert.HasCount(0, controller.Results);
            Assert.IsNull(controller.Summary);
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
        }

        [TestMethod]
        public async Task Run_NonSuccessWorkerResult_CleansStrayRunOwnedTemp()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.FailForSourceContains = "drawing1";
            launcher.WriteTempForFailedItems = true;

            await controller.RunAsync();

            Assert.AreEqual(FileResultStatus.Failed, controller.Results[0].Status);
            Assert.AreEqual(FileResultStatus.Success, controller.Results[1].Status);
            // Worker 报告失败却残留的临时 PDF 属于本次运行自有垃圾，必须清理。
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
        }

        [TestMethod]
        public async Task Run_FatalErrorWithNoFileResults_AllItemsFailWithoutPublication()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.ResultFatalError = "批处理运行期间发生致命错误（模拟）";
            launcher.ResultFileCountLimit = 0;

            await controller.RunAsync();

            Assert.HasCount(2, controller.Results);
            Assert.IsTrue(controller.Results.All(r => r.Status == FileResultStatus.Failed));
            Assert.IsTrue(controller.Summary!.HasFailures);
            StringAssert.Contains(controller.ProgressText, "致命");
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.pdf"));
        }

        [TestMethod]
        public async Task Run_FatalErrorAfterOneSuccess_PublishesOnlyTheTrustedPrefix()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            launcher.ResultFatalError = "批处理在后续文件上发生致命错误（模拟）";
            launcher.ResultFileCountLimit = 1;

            await controller.RunAsync();

            Assert.AreEqual(FileResultStatus.Success, controller.Results[0].Status);
            Assert.IsTrue(File.Exists(Path.Combine(scratch, "in", "drawing1.pdf")));
            Assert.AreEqual(FileResultStatus.Failed, controller.Results[1].Status);
            StringAssert.Contains(controller.Results[1].Message, "致命");
            Assert.IsFalse(File.Exists(Path.Combine(scratch, "in", "drawing2.pdf")));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(scratch, "in"), "*.tmp.pdf"));
            StringAssert.Contains(controller.ProgressText, "致命");
            Assert.IsTrue(controller.Summary!.HasFailures);
        }

        [TestMethod]
        public void ProgressAndEvents_StateChangeRaisedAroundRun()
        {
            var controller = NewController();
            controller.ScanFolderPath = Path.Combine(scratch, "in");
            var changes = 0;
            controller.StateChanged += () => changes++;

            var run = controller.RunAsync();
            launcher.WaitGate.Task.Wait(5000);
            launcher.WaitGate.TrySetResult(true);
            run.Wait(10000);

            Assert.IsGreaterThanOrEqualTo(2, changes);
        }

        private sealed class NullSink : ILogSink
        {
            public void Write(string message)
            {
            }
        }

        private sealed class StubInspector : IPdfInspector
        {
            private readonly int pageCount;

            /// <summary>发布校验回调：用于在发布进行中注入取消请求等测试时序。</summary>
            public Action? OnInspect { get; set; }

            public StubInspector(int pageCount) => this.pageCount = pageCount;

            public PdfInspection Inspect(string path)
            {
                OnInspect?.Invoke();
                var length = new FileInfo(path).Length;
                var header = ReadHeader(path);
                return new PdfInspection
                {
                    HasPdfHeader = header,
                    Length = length,
                    PageCount = header ? pageCount : 0
                };
            }

            private static bool ReadHeader(string path)
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(stream);
                if (stream.Length < 5)
                {
                    return false;
                }

                return new string(reader.ReadChars(5)) == "%PDF-";
            }
        }

        /// <summary>
        /// Fakes the worker process: reads the written job.json, optionally
        /// writes temp PDFs, and returns the job result the real worker would
        /// write, without launching NX.
        /// </summary>
        private sealed class StubNxDiscovery : INxInstallationDiscoveryService
        {
            public StubNxDiscovery(NxInstallation installation)
            {
                Result = new NxInstallationDiscoveryResult(
                    new[] { installation },
                    Array.Empty<NxDetectionIssue>(),
                    installation);
            }

            public NxInstallationDiscoveryResult Result { get; set; }

            public Task<NxInstallationDiscoveryResult> DetectAsync(string? savedRoot, System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(Result);

            public NxInstallationDiscoveryResult ValidateManual(string rootDirectory) => Result;
        }

        private sealed class StubNxSettings : INxSettingsStore
        {
            public Task<NxSettingsLoadResult> LoadAsync(System.Threading.CancellationToken cancellationToken) =>
                Task.FromResult(new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.Missing));

            public Task SaveAsync(NxSettings settings, System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeWorkerLauncher : IWorkerProcessLauncher
        {
            public List<WorkerLaunchRequest> LaunchCalls { get; } = new List<WorkerLaunchRequest>();
            public TaskCompletionSource<bool> WaitGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public string? FailForSourceContains { get; set; }
            public FileResultStatus? ReportStatusForAll { get; set; }
            public bool WriteGarbageTempPdfs { get; set; }

            /// <summary>模拟 Worker 报告失败却残留了本次运行自有的临时 PDF。</summary>
            public bool WriteTempForFailedItems { get; set; }

            /// <summary>
            /// 模拟真实 Worker 的文件边界取消：条目 1 完成后等待测试写入取消
            /// 标志，再按状态机语义把后续条目标记为 Cancelled。
            /// </summary>
            public bool ModelWorkerSideCancellation { get; set; }

            /// <summary>条目 1 完成（临时 PDF 与结果均已产生）的信号。</summary>
            public TaskCompletionSource<bool> FirstItemDone { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>测试已写入取消标志、Worker 可以继续的放行门。</summary>
            public TaskCompletionSource<bool> CancelGate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>模拟 Worker 写出损坏的 result.json（结果不可读）。</summary>
            public bool WriteInvalidResultJson { get; set; }

            /// <summary>模拟 Worker 写出属于其他运行的结果文件。</summary>
            public string? ResultRunIdOverride { get; set; }

            /// <summary>模拟批级致命错误：结果携带 FatalError。</summary>
            public string? ResultFatalError { get; set; }

            /// <summary>模拟 Worker 只完成前 N 个文件后崩溃（其余条目无结果、无临时 PDF）。</summary>
            public int? ResultFileCountLimit { get; set; }

            /// <summary>模拟真实 Worker 的启动耗时；仅取消/进度类测试需要。</summary>
            public bool HoldUntilReleased { get; set; }

            public WorkerLaunchResult Launch(WorkerLaunchRequest request)
            {
                LaunchCalls.Add(request);
                var job = JobJsonSerializer.ReadFromFile<JobRequest>(request.JobPath);
                var files = new List<FileResult>();
                var reported = 0;
                var cancellationObserved = false;
                foreach (var item in job.Items)
                {
                    // 与真实 Worker 的状态机一致：Skip 策略下已存在的目标
                    // 直接跳过，不写临时 PDF。
                    if (job.ExistingPdfPolicy == ExistingPdfPolicy.Skip && File.Exists(item.FinalOutputPath))
                    {
                        files.Add(new FileResult
                        {
                            SourcePath = item.SourcePath,
                            FinalOutputPath = item.FinalOutputPath,
                            Status = FileResultStatus.SkippedExisting,
                            Message = "已存在，已跳过。"
                        });
                        continue;
                    }

                    // 与真实状态机一致：文件边界检查取消标志；一旦观察到，
                    // 本条目与后续条目一律 Cancelled，不产生临时 PDF。
                    if (cancellationObserved || File.Exists(job.CancellationFlagPath))
                    {
                        cancellationObserved = true;
                        files.Add(new FileResult
                        {
                            SourcePath = item.SourcePath,
                            FinalOutputPath = item.FinalOutputPath,
                            Status = FileResultStatus.Cancelled,
                            Message = "已取消，未开始。"
                        });
                        continue;
                    }

                    if (ReportStatusForAll is not null)
                    {
                        files.Add(new FileResult
                        {
                            SourcePath = item.SourcePath,
                            FinalOutputPath = item.FinalOutputPath,
                            Status = ReportStatusForAll.Value,
                            Message = ReportStatusForAll == FileResultStatus.PureModel ? "纯模型部件，没有图纸页。" : ""
                        });
                        continue;
                    }

                    if (FailForSourceContains is not null && item.SourcePath.Contains(FailForSourceContains, StringComparison.Ordinal))
                    {
                        if (WriteTempForFailedItems)
                        {
                            File.WriteAllBytes(item.WorkerTempOutputPath, TestPdfFactory.CreateSinglePage());
                        }

                        files.Add(new FileResult
                        {
                            SourcePath = item.SourcePath,
                            FinalOutputPath = item.FinalOutputPath,
                            Status = FileResultStatus.Failed,
                            Message = "缺失模型依赖。"
                        });
                        continue;
                    }

                    var includeThisResult = ResultFileCountLimit is null || reported < ResultFileCountLimit.Value;
                    if (includeThisResult)
                    {
                        reported++;
                        if (WriteGarbageTempPdfs)
                        {
                            File.WriteAllText(item.WorkerTempOutputPath, "not a pdf");
                        }
                        else
                        {
                            File.WriteAllBytes(item.WorkerTempOutputPath, TestPdfFactory.CreateSinglePage());
                        }

                        files.Add(WorkerSuccess(item, new[] { "sheet-token" }));
                    }

                    // 超出完成限额的条目：Worker 已崩溃，无结果也无临时文件。

                    // 模拟真实 Worker：条目 1 完成后暂停，等测试以用户身份
                    // 写入取消标志，再让后续条目按状态机语义被取消。
                    if (ModelWorkerSideCancellation && reported == 1 && !FirstItemDone.Task.IsCompleted)
                    {
                        FirstItemDone.TrySetResult(true);
                        Assert.IsTrue(CancelGate.Task.Wait(15000), "测试未在时限内写入取消标志。");
                    }
                }

                var result = new JobResult
                {
                    RunId = ResultRunIdOverride ?? job.RunId,
                    StartedUtc = DateTime.UtcNow,
                    EndedUtc = DateTime.UtcNow,
                    Files = files.ToArray(),
                    FatalError = ResultFatalError,
                    Cancelled = cancellationObserved
                };
                if (WriteInvalidResultJson)
                {
                    File.WriteAllText(job.ResultPath, "{ 这不是合法的 JSON");
                }
                else
                {
                    JobJsonSerializer.WriteToFile(result, job.ResultPath);
                }

                if (HoldUntilReleased)
                {
                    WaitGate.Task.Wait(10000);
                }

                return new WorkerLaunchResult { ExitCode = 0 };
            }
        }
    }
}
