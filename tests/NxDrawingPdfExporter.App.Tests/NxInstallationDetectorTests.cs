using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class NxInstallationDetectorTests
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
    public void ValidateAndDeduplicate_ValidCustomRoots_MergeSourcesCaseInsensitively()
    {
        string root = CreateNxLayout("西门子 NX 10");
        var sut = new NxInstallationValidator(_ => "10.0.0.24");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(new[]
        {
            new NxInstallationCandidate(root + Path.DirectorySeparatorChar, NxCandidateSource.Registry),
            new NxInstallationCandidate(root.ToUpperInvariant(), NxCandidateSource.Environment),
        });

        Assert.HasCount(1, result.Installations);
        Assert.AreEqual(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)), result.Installations[0].RootDirectory);
        CollectionAssert.AreEquivalent(
            new[] { NxCandidateSource.Registry, NxCandidateSource.Environment },
            result.Installations[0].Sources.ToArray());
        Assert.IsNull(result.SelectedInstallation);
    }

    [TestMethod]
    public void ValidateAndDeduplicate_MissingRoot_ReportsRootMissing()
    {
        string missingRoot = Path.Combine(CreateScratchDirectory(), "missing NX");
        var sut = new NxInstallationValidator(_ => "10.0.0.24");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(missingRoot, NxCandidateSource.Manual) });

        Assert.IsEmpty(result.Installations);
        Assert.HasCount(1, result.Issues);
        Assert.AreEqual(NxDetectionIssueCode.RootMissing, result.Issues[0].Code);
    }

    [TestMethod]
    public void ValidateAndDeduplicate_MissingLauncher_ReportsLauncherMissing()
    {
        string root = CreateScratchDirectory();
        Directory.CreateDirectory(Path.Combine(root, "UGII", "managed"));
        File.WriteAllBytes(Path.Combine(root, "UGII", "managed", "NXOpen.dll"), Array.Empty<byte>());
        var sut = new NxInstallationValidator(_ => "10.0.0.24");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });

        Assert.AreEqual(NxDetectionIssueCode.LauncherMissing, result.Issues.Single().Code);
    }

    [TestMethod]
    public void ValidateAndDeduplicate_MissingNxOpen_ReportsNxOpenMissing()
    {
        string root = CreateScratchDirectory();
        Directory.CreateDirectory(Path.Combine(root, "UGII"));
        File.WriteAllBytes(Path.Combine(root, "UGII", "run_managed.exe"), Array.Empty<byte>());
        var sut = new NxInstallationValidator(_ => "10.0.0.24");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });

        Assert.AreEqual(NxDetectionIssueCode.NxOpenMissing, result.Issues.Single().Code);
    }

    [TestMethod]
    public void ValidateAndDeduplicate_UnreadableVersion_ReportsVersionUnreadable()
    {
        string root = CreateNxLayout("unreadable");
        var sut = new NxInstallationValidator(_ => null);

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });

        Assert.AreEqual(NxDetectionIssueCode.VersionUnreadable, result.Issues.Single().Code);
    }

    [TestMethod]
    public void ValidateAndDeduplicate_WrongDllVersion_IsRejected()
    {
        string root = CreateNxLayout("NX wrong version");
        var sut = new NxInstallationValidator(_ => "12.0.0.1");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });

        Assert.IsEmpty(result.Installations);
        Assert.HasCount(1, result.Issues);
        Assert.AreEqual(NxDetectionIssueCode.UnsupportedVersion, result.Issues[0].Code);
        StringAssert.Contains(result.Issues[0].UserMessage, "10.0.0.24");
    }

    [TestMethod]
    public void ValidateAndDeduplicate_QuotedTrailingPathWithSpaces_IsNormalized()
    {
        string root = CreateNxLayout("NX with spaces");
        var sut = new NxInstallationValidator(_ => "10.0.0.24");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(new[]
        {
            new NxInstallationCandidate($"  \"{root}{Path.DirectorySeparatorChar}\"  ", NxCandidateSource.Manual),
        });

        Assert.HasCount(1, result.Installations);
        Assert.AreEqual(Path.GetFullPath(root), result.Installations.Single().RootDirectory);
    }

    [TestMethod]
    public void ValidateAndDeduplicate_LongChineseRoot_IsAccepted()
    {
        string root = CreateNxLayout(string.Join("-", Enumerable.Repeat("长路径西门子", 12)));
        var sut = new NxInstallationValidator(_ => "10.0.0.24");

        NxInstallationDiscoveryResult result = sut.ValidateAndDeduplicate(
            new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });

        Assert.HasCount(1, result.Installations);
        Assert.AreEqual("10.0.0.24", result.Installations.Single().DetectedVersion);
    }

    private string CreateNxLayout(string name)
    {
        string root = Path.Combine(CreateScratchDirectory(), name);
        string ugii = Path.Combine(root, "UGII");
        Directory.CreateDirectory(Path.Combine(ugii, "managed"));
        File.WriteAllBytes(Path.Combine(ugii, "run_managed.exe"), Array.Empty<byte>());
        File.WriteAllBytes(Path.Combine(ugii, "managed", "NXOpen.dll"), Array.Empty<byte>());
        return root;
    }

    private string CreateScratchDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "nxpdf-nxdetect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        scratchDirectories.Add(directory);
        return directory;
    }
}
