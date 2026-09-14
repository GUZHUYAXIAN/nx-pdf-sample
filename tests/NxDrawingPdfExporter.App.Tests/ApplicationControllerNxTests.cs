using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;
using NxDrawingPdfExporter.App.Logging;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class ApplicationControllerNxTests
{
    [TestMethod]
    public async Task Detection_LogWriteFailure_KeepsSelectionAndShowsWarning()
    {
        var installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var result = ReadyResult(installation) with { Issues = new[] {
            new NxDetectionIssue(NxDetectionIssueCode.CandidateSourceFailed, "source failed", NxCandidateSource.Registry) } };
        var controller = new ApplicationController(new ApplicationServices
        {
            NxInstallationDiscovery = new StubDiscovery(result), NxSettings = new StubSettings(),
        }, new StubLog(), new FailingLog());
        await controller.RefreshNxStatusAsync();
        Assert.AreEqual(installation, controller.SelectedNx);
        StringAssert.Contains(controller.NxStatusMessage, "无法保存 NX 检测诊断");
        Assert.IsFalse(controller.IsNxDetectionBusy);
    }

    [TestMethod]
    public void DetectionLog_LazilyCreatesPersistentLocalLog()
    {
        string directory = Path.Combine(Path.GetTempPath(), "nxpdf-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new NxDetectionLog(directory);
            Assert.IsFalse(Directory.Exists(directory));
            sink.Write("NX detection: code=CandidateSourceFailed; source=Registry; exception=IOException");
            StringAssert.Contains(File.ReadAllText(Path.Combine(directory, "run.log")), "source=Registry");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Detection_Issues_AreLoggedEvenWithReadySelection(bool manual)
    {
        var installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var issue = new NxDetectionIssue(NxDetectionIssueCode.CandidateSourceFailed,
            "private user message", NxCandidateSource.Registry, "UnauthorizedAccessException");
        var result = ReadyResult(installation) with { Issues = new[] { issue } };
        var log = new StubLog();
        var controller = new ApplicationController(new ApplicationServices
        {
            NxInstallationDiscovery = new StubDiscovery(result), NxSettings = new StubSettings(),
        }, log);
        if (manual) await controller.SelectManualNxAsync(installation.RootDirectory);
        else await controller.RefreshNxStatusAsync();
        string messages = string.Join("\n", log.Messages);
        StringAssert.Contains(messages, "CandidateSourceFailed");
        StringAssert.Contains(messages, "Registry");
        StringAssert.Contains(messages, "UnauthorizedAccessException");
        Assert.DoesNotContain("private user message", messages);
        Assert.DoesNotContain(@"E:\NX", messages);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Selection_SavePending_BlocksOtherNxActionsAndStart(bool manual)
    {
        var installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var settings = new StubSettings();
        var controller = NewController(new StubDiscovery(ReadyResult(installation)), settings);
        await controller.RefreshNxStatusAsync();
        settings.SaveGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var states = new List<bool>();
        controller.StateChanged += () => states.Add(controller.IsNxDetectionBusy);
        Task selection = manual ? controller.SelectManualNxAsync(installation.RootDirectory)
            : controller.SelectDetectedNxAsync(installation.RootDirectory);
        try
        {
            Assert.IsTrue(controller.IsNxDetectionBusy);
            Assert.IsFalse(MainForm.BuildNxViewState(controller).EnableStart);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.RunAsync());
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.RefreshNxStatusAsync());
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.SelectManualNxAsync(installation.RootDirectory));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.SelectDetectedNxAsync(installation.RootDirectory));
            Assert.IsNull(controller.CurrentRunDirectory);
        }
        finally { settings.SaveGate.TrySetResult(); await selection; }
        Assert.IsFalse(controller.IsNxDetectionBusy);
        Assert.IsTrue(states[0]);
        Assert.IsFalse(states[^1]);
        Assert.IsTrue(MainForm.BuildNxViewState(controller).EnableStart);
    }

    [TestMethod]
    public async Task Refresh_Ready_ShowsRootAndSavesValidatedSelection()
    {
        var installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var settings = new StubSettings();
        var controller = NewController(new StubDiscovery(ReadyResult(installation)), settings);
        await controller.RefreshNxStatusAsync();
        StringAssert.Contains(controller.NxStatusMessage, @"E:\NX");
        Assert.AreEqual(@"E:\NX", settings.Saved?.NxRootDirectory);
    }

    [TestMethod]
    public async Task Refresh_InProgress_RejectsRunAndOverlappingRefresh()
    {
        var discovery = new StubDiscovery(EmptyResult()) { Gate = new TaskCompletionSource<NxInstallationDiscoveryResult>() };
        var controller = NewController(discovery, new StubSettings());
        Task refresh = controller.RefreshNxStatusAsync();
        try
        {
            Assert.IsTrue(controller.IsNxDetectionBusy);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.RunAsync());
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.RefreshNxStatusAsync());
        }
        finally { discovery.Gate.TrySetResult(EmptyResult()); await refresh; }
    }

    [TestMethod]
    public async Task ManualFailure_ShowsSpecificReasonWithoutSaving()
    {
        var settings = new StubSettings();
        var discovery = new StubDiscovery(EmptyResult()) { ManualResult = new(
            Array.Empty<NxInstallation>(), new[] { new NxDetectionIssue(NxDetectionIssueCode.LauncherMissing, "缺少 UGII 启动器。") }, null) };
        var controller = NewController(discovery, settings);
        await controller.SelectManualNxAsync(@"E:\invalid");
        StringAssert.Contains(controller.NxStatusMessage, "缺少 UGII 启动器");
        Assert.IsNull(settings.Saved);
    }

    [TestMethod]
    public async Task Run_SelectionBecameInvalid_RefreshesAndDoesNotCreateJob()
    {
        var discovery = new StubDiscovery(ReadyResult(Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe")));
        var controller = NewController(discovery, new StubSettings());
        await controller.RefreshNxStatusAsync();
        discovery.ManualResult = EmptyResult();
        discovery.Result = EmptyResult();
        await controller.RunAsync();
        Assert.IsNull(controller.SelectedNx);
        Assert.IsNull(controller.CurrentRunDirectory);
        Assert.AreEqual(2, discovery.DetectCalls);
    }

    [TestMethod]
    public async Task RefreshNxStatusAsync_UsesSavedRootAndPublishesReadyState()
    {
        NxInstallation installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var discovery = new StubDiscovery(ReadyResult(installation));
        var settings = new StubSettings(new NxSettingsLoadResult(
            new NxSettings(1, @"E:\NX"), NxSettingsLoadStatus.Loaded));
        var controller = NewController(discovery, settings);

        await controller.RefreshNxStatusAsync();

        Assert.AreEqual(@"E:\NX", discovery.LastSavedRoot);
        Assert.AreEqual(@"E:\NX", controller.SelectedNx!.RootDirectory);
        Assert.IsFalse(controller.IsNxDetectionBusy);
    }

    [TestMethod]
    public async Task RunAsync_WithoutValidatedSelection_BlocksBeforeJobCreation()
    {
        var controller = NewController(new StubDiscovery(EmptyResult()), new StubSettings());
        controller.ScanFolderPath = Path.GetTempPath();

        await controller.RunAsync();

        StringAssert.Contains(controller.ProgressText, "没有经过验证的 NX");
        Assert.IsNull(controller.CurrentRunDirectory);
    }

    [TestMethod]
    public async Task SelectDetectedNxAsync_PersistsValidatedCandidate()
    {
        NxInstallation installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var discovery = new StubDiscovery(new NxInstallationDiscoveryResult(
            new[] { installation }, Array.Empty<NxDetectionIssue>(), null));
        var settings = new StubSettings();
        var controller = NewController(discovery, settings);

        await controller.RefreshNxStatusAsync();
        await controller.SelectDetectedNxAsync(installation.RootDirectory);

        Assert.AreEqual(installation.RootDirectory, controller.SelectedNx!.RootDirectory);
        Assert.AreEqual(installation.RootDirectory, settings.Saved!.NxRootDirectory);
    }

    [TestMethod]
    public async Task SelectManualNxAsync_InvalidSelection_PreservesCurrentValidatedSelection()
    {
        NxInstallation installation = Ready(@"E:\NX", @"E:\NX\UGII\run_managed.exe");
        var discovery = new StubDiscovery(ReadyResult(installation))
        {
            ManualResult = EmptyResult(),
        };
        var controller = NewController(discovery, new StubSettings());
        await controller.RefreshNxStatusAsync();

        await controller.SelectManualNxAsync(@"E:\not-an-nx");

        Assert.AreEqual(installation.RootDirectory, controller.SelectedNx!.RootDirectory);
    }

    private static ApplicationController NewController(StubDiscovery discovery, StubSettings settings) => new(
        new ApplicationServices
        {
            NxInstallationDiscovery = discovery,
            NxSettings = settings,
        },
        new StubLog());

    private static NxInstallation Ready(string root, string launcher) => new(
        root,
        launcher,
        Path.Combine(root, "UGII", "managed", "NXOpen.dll"),
        "10.0.0.24",
        new[] { NxCandidateSource.Registry });

    private static NxInstallationDiscoveryResult ReadyResult(NxInstallation installation) => new(
        new[] { installation }, Array.Empty<NxDetectionIssue>(), installation);

    private static NxInstallationDiscoveryResult EmptyResult() => new(
        Array.Empty<NxInstallation>(), Array.Empty<NxDetectionIssue>(), null);

    private sealed class StubDiscovery : INxInstallationDiscoveryService
    {
        public NxInstallationDiscoveryResult Result { get; set; }
        public TaskCompletionSource<NxInstallationDiscoveryResult>? Gate { get; set; }
        public int DetectCalls { get; private set; }

        public StubDiscovery(NxInstallationDiscoveryResult result)
        {
            Result = result;
        }

        public string? LastSavedRoot { get; private set; }

        public NxInstallationDiscoveryResult? ManualResult { get; set; }

        public Task<NxInstallationDiscoveryResult> DetectAsync(string? savedRoot, CancellationToken cancellationToken)
        {
            LastSavedRoot = savedRoot;
            DetectCalls++;
            return Gate?.Task ?? Task.FromResult(Result);
        }

        public NxInstallationDiscoveryResult ValidateManual(string rootDirectory) => ManualResult ?? Result;
    }

    private sealed class StubSettings : INxSettingsStore
    {
        private readonly NxSettingsLoadResult loadResult;

        public StubSettings(NxSettingsLoadResult? loadResult = null)
        {
            this.loadResult = loadResult ?? new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.Missing);
        }

        public NxSettings? Saved { get; private set; }
        public TaskCompletionSource? SaveGate { get; set; }

        public Task<NxSettingsLoadResult> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(loadResult);

        public async Task SaveAsync(NxSettings settings, CancellationToken cancellationToken)
        {
            if (SaveGate is not null) await SaveGate.Task.WaitAsync(cancellationToken);
            Saved = settings;
        }
    }

    private sealed class StubLog : ILogSink
    {
        public List<string> Messages { get; } = new();
        public void Write(string message)
        {
            Messages.Add(message);
        }
    }

    private sealed class FailingLog : ILogSink
    {
        public void Write(string message) => throw new IOException("private path");
    }
}
