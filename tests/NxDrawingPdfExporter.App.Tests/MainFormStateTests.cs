using System;
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
public sealed class MainFormStateTests
{
    [TestMethod]
    [DataRow(@"c:\nx-a", @"E:\NX-B", @"C:\NX-A")]
    [DataRow(@"F:\removed", @"E:\NX-B", @"E:\NX-B")]
    [DataRow(null, @"E:\NX-B", @"E:\NX-B")]
    [DataRow(null, null, @"C:\NX-A")]
    public void ChooseNxCandidate_PreservesHighlightOrFallsBackToActive(string? highlighted, string? active, string expected)
    {
        var first = Installation(@"C:\NX-A");
        var second = Installation(@"E:\NX-B");
        var discovery = new NxInstallationDiscoveryResult(new[] { first, second },
            Array.Empty<NxDetectionIssue>(), active is null ? null : second);
        var candidate = MainForm.ChooseNxCandidate(discovery, highlighted);
        Assert.AreEqual(expected, candidate!.RootDirectory);
        Assert.IsTrue(ReferenceEquals(first, candidate) || ReferenceEquals(second, candidate));
    }

    [TestMethod]
    public void ChooseNxCandidate_EmptyResult_DiscardsOldHighlight()
    {
        var discovery = new NxInstallationDiscoveryResult(Array.Empty<NxInstallation>(),
            Array.Empty<NxDetectionIssue>(), null);
        Assert.IsNull(MainForm.ChooseNxCandidate(discovery, @"E:\removed"));
    }

    [TestMethod]
    public void FormatNxCandidate_ShowsRootAndValidatedVersion()
    {
        string label = MainForm.FormatNxCandidate(Installation(@"E:\西门子 NX"));
        StringAssert.Contains(label, @"E:\西门子 NX");
        StringAssert.Contains(label, "10.0.0.24");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BuildNxViewState_ChosenFromMultiple_StillAllowsCandidateSwitch(bool savedSelection)
    {
        var first = Installation(@"C:\NX-A");
        var second = Installation(@"E:\NX-B");
        var controller = await CreateControllerAsync(new NxInstallationDiscoveryResult(
            new[] { first, second }, Array.Empty<NxDetectionIssue>(), savedSelection ? second : null));
        if (!savedSelection) await controller.SelectDetectedNxAsync(second.RootDirectory);

        var state = MainForm.BuildNxViewState(controller);
        Assert.IsTrue(state.ShowCandidateList);
        Assert.IsTrue(state.EnableNxActions);
        Assert.IsTrue(state.EnableStart);
        Assert.AreEqual(second.RootDirectory, controller.SelectedNx!.RootDirectory);
    }

    [TestMethod]
    public async Task BuildNxViewState_MultipleCandidates_DisablesStartAndShowsSelection()
    {
        var controller = await CreateControllerAsync(new NxInstallationDiscoveryResult(
            new[] { Installation(@"C:\NX-A"), Installation(@"E:\NX-B") },
            Array.Empty<NxDetectionIssue>(),
            null));

        MainFormNxViewState state = MainForm.BuildNxViewState(controller);

        Assert.IsTrue(state.ShowCandidateList);
        Assert.IsFalse(state.EnableStart);
        Assert.IsTrue(state.EnableNxActions);
    }

    private static async Task<ApplicationController> CreateControllerAsync(NxInstallationDiscoveryResult result)
    {
        var controller = new ApplicationController(new ApplicationServices
        {
            NxInstallationDiscovery = new StubDiscovery(result),
            NxSettings = new StubSettings(),
        }, new NullLog());
        await controller.RefreshNxStatusAsync();
        return controller;
    }

    private static NxInstallation Installation(string root) => new(
        root,
        Path.Combine(root, "UGII", "run_managed.exe"),
        Path.Combine(root, "UGII", "managed", "NXOpen.dll"),
        "10.0.0.24",
        new[] { NxCandidateSource.Registry });

    private sealed class StubDiscovery(NxInstallationDiscoveryResult result) : INxInstallationDiscoveryService
    {
        public Task<NxInstallationDiscoveryResult> DetectAsync(string? savedRoot, CancellationToken cancellationToken) => Task.FromResult(result);
        public NxInstallationDiscoveryResult ValidateManual(string rootDirectory) => result;
    }

    private sealed class StubSettings : INxSettingsStore
    {
        public Task<NxSettingsLoadResult> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(new NxSettingsLoadResult(new NxSettings(), NxSettingsLoadStatus.Missing));
        public Task SaveAsync(NxSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullLog : ILogSink
    {
        public void Write(string message) { }
    }
}
