using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class NxInstallationCandidateSourceTests
{
    [TestMethod]
    public async Task EnvironmentSource_QuotedUgiiRoot_ReturnsParentNxRoot()
    {
        var source = new EnvironmentNxCandidateSource(name =>
            name == "UGII_ROOT_DIR" ? "  \"E:\\西门子 NX\\UGII\\\"  " : null);
        var candidates = await source.GetCandidatesAsync(CancellationToken.None);
        Assert.HasCount(1, candidates);
        Assert.AreEqual(@"E:\西门子 NX", candidates[0].RootDirectory);
    }

    [TestMethod]
    public async Task EnvironmentSource_UgiiRootDirectory_ReturnsParentNxRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "NX Custom");
        string ugii = Path.Combine(root, "UGII") + Path.DirectorySeparatorChar;
        var source = new EnvironmentNxCandidateSource(name =>
            name == "UGII_ROOT_DIR" ? ugii : null);

        IReadOnlyList<NxInstallationCandidate> candidates =
            await source.GetCandidatesAsync(CancellationToken.None);

        Assert.HasCount(1, candidates);
        Assert.AreEqual(root, candidates[0].RootDirectory);
        Assert.AreEqual(NxCandidateSource.Environment, candidates[0].Source);
    }

    [TestMethod]
    public async Task EnvironmentSource_BaseAndRoot_ReturnsBothCandidates()
    {
        var source = new EnvironmentNxCandidateSource(name => name switch
        {
            "UGII_BASE_DIR" => @"E:\NX base",
            "UGII_ROOT_DIR" => @"F:\NX root\UGII",
            _ => null,
        });

        IReadOnlyList<NxInstallationCandidate> candidates =
            await source.GetCandidatesAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { @"E:\NX base", @"F:\NX root" },
            candidates.Select(candidate => candidate.RootDirectory).ToArray());
    }

    [TestMethod]
    public async Task RegistrySource_QueriesOnlyBothApprovedViews()
    {
        var queriedViews = new List<RegistryView>();
        var source = new RegistryNxCandidateSource(view =>
        {
            queriedViews.Add(view);
            return view == RegistryView.Registry64 ? new[] { @"E:\NX Registry" } : Array.Empty<string>();
        });

        IReadOnlyList<NxInstallationCandidate> candidates =
            await source.GetCandidatesAsync(CancellationToken.None);

        CollectionAssert.AreEquivalent(
            new[] { RegistryView.Registry64, RegistryView.Registry32 },
            queriedViews);
        Assert.HasCount(1, candidates);
        Assert.AreEqual(@"E:\NX Registry", candidates[0].RootDirectory);
        Assert.AreEqual(NxCandidateSource.Registry, candidates[0].Source);
    }

    [TestMethod]
    public async Task InstalledApplicationSource_AcceptsOnlySiemensNx10Entries()
    {
        var source = new InstalledApplicationNxCandidateSource(view => new[]
        {
            new InstalledApplicationEntry("Siemens NX 10.0", @"E:\NX Accepted"),
            new InstalledApplicationEntry("Siemens NX 12", @"E:\NX Rejected"),
            new InstalledApplicationEntry("Unrelated", @"E:\Other"),
        });

        IReadOnlyList<NxInstallationCandidate> candidates =
            await source.GetCandidatesAsync(CancellationToken.None);

        Assert.HasCount(2, candidates);
        CollectionAssert.AreEquivalent(
            new[] { @"E:\NX Accepted", @"E:\NX Accepted" },
            candidates.Select(candidate => candidate.RootDirectory).ToArray());
        Assert.IsTrue(candidates.All(candidate => candidate.Source == NxCandidateSource.InstalledApplication));
    }

    [TestMethod]
    public async Task EnvironmentSource_NoInstallationSignals_DoesNotInventCandidate()
    {
        var source = new EnvironmentNxCandidateSource(_ => null);

        IReadOnlyList<NxInstallationCandidate> candidates =
            await source.GetCandidatesAsync(CancellationToken.None);

        Assert.IsEmpty(candidates);
    }
}
