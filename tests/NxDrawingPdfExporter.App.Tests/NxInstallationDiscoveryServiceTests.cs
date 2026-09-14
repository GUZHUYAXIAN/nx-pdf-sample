using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class NxInstallationDiscoveryServiceTests
{
    private readonly List<string> scratchDirectories = new();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (string directory in scratchDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    [DataRow(NxCandidateSource.Registry)]
    [DataRow(NxCandidateSource.InstalledApplication)]
    [DataRow(NxCandidateSource.Environment)]
    public async Task DetectAsync_SourceFailure_IdentifiesSourceWithoutPrivateException(NxCandidateSource source)
    {
        INxInstallationCandidateSource failing = source switch
        {
            NxCandidateSource.Registry => new RegistryNxCandidateSource(_ => throw new UnauthorizedAccessException("private path")),
            NxCandidateSource.InstalledApplication => new InstalledApplicationNxCandidateSource(_ => throw new UnauthorizedAccessException("private path")),
            _ => new EnvironmentNxCandidateSource(_ => throw new UnauthorizedAccessException("private path")),
        };
        var result = await NewService(failing).DetectAsync(null, CancellationToken.None);
        Assert.AreEqual(source, result.Issues.Single().Source);
        Assert.AreEqual("UnauthorizedAccessException", result.Issues.Single().Diagnostic);
        Assert.DoesNotContain("private path", result.Issues.Single().UserMessage);
    }

    [TestMethod]
    public async Task DetectAsync_MultipleValidCandidates_SelectsValidSavedRoot()
    {
        string first = CreateNxLayout("NX-A");
        string second = CreateNxLayout("NX-B");
        var sut = NewService(new StubCandidateSource(
            new NxInstallationCandidate(first, NxCandidateSource.Registry),
            new NxInstallationCandidate(second, NxCandidateSource.Environment)));

        NxInstallationDiscoveryResult result =
            await sut.DetectAsync(second + Path.DirectorySeparatorChar, CancellationToken.None);

        Assert.AreEqual(second, result.SelectedInstallation!.RootDirectory);
        Assert.IsFalse(result.RequiresSelection);
    }

    [TestMethod]
    public async Task DetectAsync_SourceThrows_ContinuesAndReportsSanitizedIssue()
    {
        string validRoot = CreateNxLayout("valid");
        var throwing = new ThrowingCandidateSource(new UnauthorizedAccessException("private path"));
        var valid = new StubCandidateSource(new NxInstallationCandidate(validRoot, NxCandidateSource.Environment));
        var sut = NewService(throwing, valid);

        NxInstallationDiscoveryResult result = await sut.DetectAsync(null, CancellationToken.None);

        Assert.IsTrue(result.IsReady);
        Assert.AreEqual(NxDetectionIssueCode.CandidateSourceFailed, result.Issues.Single().Code);
        Assert.DoesNotContain("private path", result.Issues.Single().UserMessage);
    }

    [TestMethod]
    public async Task DetectAsync_NoCandidates_ReturnsNoSelection()
    {
        var sut = NewService(new StubCandidateSource());

        NxInstallationDiscoveryResult result = await sut.DetectAsync(null, CancellationToken.None);

        Assert.IsEmpty(result.Installations);
        Assert.IsNull(result.SelectedInstallation);
        Assert.IsFalse(result.IsReady);
    }

    [TestMethod]
    public async Task DetectAsync_MultipleCandidatesWithoutSavedMatch_RequiresSelection()
    {
        string first = CreateNxLayout("one");
        string second = CreateNxLayout("two");
        var sut = NewService(new StubCandidateSource(
            new NxInstallationCandidate(first, NxCandidateSource.Registry),
            new NxInstallationCandidate(second, NxCandidateSource.Environment)));

        NxInstallationDiscoveryResult result = await sut.DetectAsync(null, CancellationToken.None);

        Assert.IsNull(result.SelectedInstallation);
        Assert.IsTrue(result.RequiresSelection);
    }

    [TestMethod]
    public async Task DetectAsync_InvalidSavedRoot_DoesNotBlockLaterCandidate()
    {
        string validRoot = CreateNxLayout("valid");
        string invalidRoot = Path.Combine(CreateScratchDirectory(), "missing");
        var sut = NewService(new StubCandidateSource(
            new NxInstallationCandidate(validRoot, NxCandidateSource.Registry)));

        NxInstallationDiscoveryResult result = await sut.DetectAsync(invalidRoot, CancellationToken.None);

        Assert.IsTrue(result.IsReady);
        Assert.AreEqual(validRoot, result.SelectedInstallation!.RootDirectory);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == NxDetectionIssueCode.RootMissing));
    }

    [TestMethod]
    public void ValidateManual_ValidRoot_SelectsUsingSharedValidator()
    {
        string root = CreateNxLayout("manual");
        var sut = NewService(new StubCandidateSource());

        NxInstallationDiscoveryResult result = sut.ValidateManual(root);

        Assert.IsTrue(result.IsReady);
        Assert.AreEqual(root, result.SelectedInstallation!.RootDirectory);
        Assert.AreEqual(NxCandidateSource.Manual, result.SelectedInstallation.Sources.Single());
    }

    [TestMethod]
    public async Task DetectAsync_Cancellation_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = NewService(new StubCandidateSource());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => sut.DetectAsync(null, cancellation.Token));
    }

    private NxInstallationDiscoveryService NewService(params INxInstallationCandidateSource[] sources) =>
        new(sources, new NxInstallationValidator(_ => "10.0.0.24"));

    private string CreateNxLayout(string name)
    {
        string root = Path.Combine(CreateScratchDirectory(), name);
        Directory.CreateDirectory(Path.Combine(root, "UGII", "managed"));
        File.WriteAllBytes(Path.Combine(root, "UGII", "run_managed.exe"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(root, "UGII", "managed", "NXOpen.dll"), Array.Empty<byte>());
        return root;
    }

    private string CreateScratchDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "nxpdf-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        scratchDirectories.Add(directory);
        return directory;
    }

    private sealed class StubCandidateSource : INxInstallationCandidateSource
    {
        private readonly IReadOnlyList<NxInstallationCandidate> candidates;

        public StubCandidateSource(params NxInstallationCandidate[] candidates)
        {
            this.candidates = candidates;
        }

        public Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(candidates);
        }
    }

    private sealed class ThrowingCandidateSource : INxInstallationCandidateSource
    {
        private readonly Exception error;

        public ThrowingCandidateSource(Exception error)
        {
            this.error = error;
        }

        public Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken) =>
            Task.FromException<IReadOnlyList<NxInstallationCandidate>>(error);
    }
}
