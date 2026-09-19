using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class DualNxTests
{
    private readonly string scratch = Path.Combine(Path.GetTempPath(), "nxpdf-dual-" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string> versions = new(StringComparer.OrdinalIgnoreCase);

    [TestCleanup]
    public void Cleanup() { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }

    private string Layout(bool modern)
    {
        string root = Path.Combine(scratch, modern ? "中文 NX 2506" : "NX 10");
        string bin = Path.Combine(root, modern ? "NXBIN" : "UGII");
        Directory.CreateDirectory(Path.Combine(bin, "managed"));
        File.WriteAllText(Path.Combine(bin, modern ? "run_dotnet_nxopen.exe" : "run_managed.exe"), "fixture");
        string[] names = { "NXOpen.dll", "NXOpen.UF.dll", "NXOpen.Utilities.dll" };
        string[] values = modern ? new[] { "2506.0.0.0", "2506.1700.0.0", "2506.0.0.0" }
            : new[] { "10.0.0.24", "10.0.0.23", "4.0.0.0" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = Path.Combine(bin, "managed", names[i]);
            File.WriteAllText(path, "fixture"); versions[path] = values[i];
        }
        return root;
    }

    private NxInstallationValidator Validator() => new(path => versions[path]);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SingleInstallation_SelectsMatchingProfile(bool modern)
    {
        string root = Layout(modern);
        var service = new NxInstallationDiscoveryService(new[] { new Source(root) }, Validator());
        var result = await service.DetectAsync(null, CancellationToken.None);
        Assert.IsTrue(result.IsReady);
        var selected = result.SelectedInstallation!;
        Assert.AreEqual(modern ? "NX 2506" : "NX 10", selected.Profile.Label);
        Assert.AreEqual(modern ? "2506.1700.0.0" : "10.0.0.23", selected.UfVersion);
        StringAssert.Contains(selected.LauncherPath, modern ? "NXBIN" : "UGII");
        StringAssert.Contains(selected.Profile.WorkerRelativePath, modern ? "nx2506" : "nx10");
    }

    [TestMethod]
    public async Task BothInstallations_RequireChoiceAndRetainSavedNx10()
    {
        string old = Layout(false), modern = Layout(true);
        var service = new NxInstallationDiscoveryService(new[] { new Source(old, modern) }, Validator());
        Assert.IsTrue((await service.DetectAsync(null, CancellationToken.None)).RequiresSelection);
        Assert.AreEqual(old, (await service.DetectAsync(old, CancellationToken.None)).SelectedInstallation!.RootDirectory);
        Assert.IsTrue((await service.DetectAsync(Path.Combine(scratch, "gone"), CancellationToken.None)).RequiresSelection);
        Assert.AreEqual(modern, service.ValidateManual(" \"" + modern + "\\\" ").SelectedInstallation!.RootDirectory);
    }

    [TestMethod]
    [DataRow("NXOpen.dll", "2506.1.0.0")]
    [DataRow("NXOpen.UF.dll", "10.0.0.23")]
    [DataRow("NXOpen.UF.dll", "2506.1800.0.0")]
    [DataRow("NXOpen.Utilities.dll", "4.0.0.0")]
    public void UnknownOrMixedComponentTuple_IsRejected(string name, string version)
    {
        string root = Layout(true);
        versions[Path.Combine(root, "NXBIN", "managed", name)] = version;
        var result = Validator().ValidateAndDeduplicate(new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) });
        Assert.IsEmpty(result.Installations);
        Assert.AreEqual(NxDetectionIssueCode.UnsupportedVersion, result.Issues.Single().Code);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void MissingUf_IsRejected(bool modern)
    {
        string root = Layout(modern);
        File.Delete(Path.Combine(root, modern ? "NXBIN" : "UGII", "managed", "NXOpen.UF.dll"));
        Assert.IsEmpty(Validator().ValidateAndDeduplicate(new[] { new NxInstallationCandidate(root, NxCandidateSource.Manual) }).Installations);
    }

    [TestMethod]
    public async Task Environment_NxbinRootIsNormalized()
    {
        var source = new EnvironmentNxCandidateSource(name => name == "UGII_ROOT_DIR" ? " \"E:\\中文 NX\\NXBIN\\\" " : null);
        Assert.AreEqual(@"E:\中文 NX", (await source.GetCandidatesAsync(CancellationToken.None)).Single().RootDirectory);
    }

    [TestMethod]
    public async Task InstalledApplications_Include2506ButExcludeUnrelatedVersions()
    {
        var source = new InstalledApplicationNxCandidateSource(_ => new[] {
            new InstalledApplicationEntry("Siemens NX 2506", @"E:\NX"),
            new InstalledApplicationEntry("Siemens NX 25060", @"E:\bad"),
            new InstalledApplicationEntry("Siemens NX 12", @"E:\bad") });
        var candidates = await source.GetCandidatesAsync(CancellationToken.None);
        Assert.HasCount(2, candidates);
        Assert.IsTrue(candidates.All(c => c.RootDirectory == @"E:\NX"));
    }

    private sealed class Source(params string[] roots) : INxInstallationCandidateSource
    {
        public Task<IReadOnlyList<NxInstallationCandidate>> GetCandidatesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NxInstallationCandidate>>(roots.Select(r => new NxInstallationCandidate(r, NxCandidateSource.Registry)).ToArray());
    }
}
