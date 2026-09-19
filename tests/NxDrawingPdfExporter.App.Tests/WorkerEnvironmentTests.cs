using NxDrawingPdfExporter.App.Runtime;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class WorkerEnvironmentTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ChildEnvironment_RemovesOtherNxPathsAndPreservesParentAndLicense(bool modern)
    {
        var profile = modern ? NxRuntimeProfile.Nx2506 : NxRuntimeProfile.Nx10;
        string root = modern ? @"E:\中文 NX 2506" : @"F:\NX 10";
        var selected = new NxInstallation(root, Path.Combine(root, profile.BinDirectoryName, profile.LauncherName),
            Path.Combine(root, profile.BinDirectoryName, "managed", "NXOpen.dll"), profile.NxOpenVersion,
            new[] { NxCandidateSource.Manual }) { Profile = profile };
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase) {
            ["UGII_BASE_DIR"] = @"C:\Other NX", ["UGII_ROOT_DIR"] = @"C:\Other NX\UGII\",
            ["UGII_CUSTOM_DIRECTORY_FILE"] = @"C:\Other NX\custom.dat",
            ["UGII_USER_DIR"] = @"C:\customizations", ["SPLM_LICENSE_SERVER"] = "preserved-license",
            ["UGII_LICENSE_FILE"] = "preserved-legacy-license",
            ["UGII_LANG"] = "simpl_chinese", ["UGII_UTF8_MODE"] = "1",
            ["PATH"] = @"C:\Windows;C:\Other NX\UGII;C:\Other NX\UGII\managed;C:\Other NX-extra;F:\Known NX\NXBIN"
        };
        var original = parent.ToArray();
        var child = WorkerEnvironment.Create(selected, parent, new[] { @"F:\Known NX" });
        CollectionAssert.AreEqual(original, parent.ToArray());
        Assert.AreEqual(root, child["UGII_BASE_DIR"]);
        Assert.AreEqual(Path.Combine(root, profile.BinDirectoryName) + "\\", child["UGII_ROOT_DIR"]);
        Assert.IsFalse(child.ContainsKey("UGII_CUSTOM_DIRECTORY_FILE"));
        Assert.IsFalse(child.ContainsKey("UGII_USER_DIR"));
        Assert.AreEqual("preserved-license", child["SPLM_LICENSE_SERVER"]);
        Assert.AreEqual("preserved-legacy-license", child["UGII_LICENSE_FILE"]);
        Assert.AreEqual("simpl_chinese", child["UGII_LANG"]);
        Assert.AreEqual("1", child["UGII_UTF8_MODE"]);
        CollectionAssert.AreEqual(new[] { Path.Combine(root, profile.BinDirectoryName), selected.ManagedDirectory,
            @"C:\Windows", @"C:\Other NX-extra" }, child["PATH"]!.Split(';'));
    }

    [TestMethod]
    public void StartInfo_BindsProfileArgumentsAndChildEnvironment()
    {
        var profile = NxRuntimeProfile.Nx2506;
        string root = @"E:\中文 NX";
        var selected = new NxInstallation(root, Path.Combine(root, "NXBIN", profile.LauncherName),
            Path.Combine(root, "NXBIN", "managed", "NXOpen.dll"), profile.NxOpenVersion,
            new[] { NxCandidateSource.Manual }) { Profile = profile };
        var request = new WorkerLaunchRequest {
            Installation = selected, NxLauncherPath = selected.LauncherPath,
            WorkerExePath = @"E:\portable 中文\worker\nx2506\NxDrawingPdfExporter.Worker.Nx2506.exe",
            RunDirectory = @"E:\run 中文", JobPath = @"E:\run 中文\job.json"
        };
        var info = WorkerProcessLauncher.CreateStartInfo(request);
        Assert.AreEqual(selected.LauncherPath, info.FileName);
        CollectionAssert.AreEqual(new[] { request.WorkerExePath, "--run-job", request.JobPath }, info.ArgumentList.ToArray());
        Assert.AreEqual(root, info.Environment["UGII_BASE_DIR"]);
        Assert.AreEqual(request.RunDirectory, info.WorkingDirectory);
        Assert.IsFalse(info.UseShellExecute);
        request.NxLauncherPath = @"E:\other\run_managed.exe";
        Assert.ThrowsExactly<InvalidOperationException>(() => WorkerProcessLauncher.CreateStartInfo(request));
    }
}
