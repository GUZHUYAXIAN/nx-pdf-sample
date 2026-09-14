using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.App.Configuration;

namespace NxDrawingPdfExporter.App.Tests;

[TestClass]
public sealed class NxSettingsStoreTests
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
    public async Task SaveThenLoad_UnicodePath_RoundTripsSchema1()
    {
        string path = SettingsPath();
        var sut = new JsonNxSettingsStore(path);

        await sut.SaveAsync(new NxSettings(1, @"E:\西门子\NX 10.0"), CancellationToken.None);
        NxSettingsLoadResult loaded = await sut.LoadAsync(CancellationToken.None);

        Assert.AreEqual(NxSettingsLoadStatus.Loaded, loaded.Status);
        Assert.AreEqual(@"E:\西门子\NX 10.0", loaded.Settings.NxRootDirectory);
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(path)!, ".settings.json.*.tmp"));
        Assert.IsTrue(File.ReadAllText(path, Encoding.UTF8).EndsWith("\n", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsMissing()
    {
        var sut = new JsonNxSettingsStore(SettingsPath());

        NxSettingsLoadResult loaded = await sut.LoadAsync(CancellationToken.None);

        Assert.AreEqual(NxSettingsLoadStatus.Missing, loaded.Status);
        Assert.IsNull(loaded.Settings.NxRootDirectory);
    }

    [TestMethod]
    public async Task LoadAsync_CancelledBeforeExistenceCheck_PropagatesCancellation()
    {
        var sut = new JsonNxSettingsStore(SettingsPath());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => sut.LoadAsync(cancellation.Token));
    }

    [TestMethod]
    public async Task LoadAsync_InvalidJson_ReturnsInvalidJsonWithoutChangingFile()
    {
        string path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] original = Encoding.UTF8.GetBytes("not json");
        await File.WriteAllBytesAsync(path, original);
        var sut = new JsonNxSettingsStore(path);

        NxSettingsLoadResult loaded = await sut.LoadAsync(CancellationToken.None);

        Assert.AreEqual(NxSettingsLoadStatus.InvalidJson, loaded.Status);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(path));
    }

    [TestMethod]
    public async Task LoadAsync_UnsupportedSchema_ReturnsUnsupportedSchema()
    {
        string path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":2,\"nxRootDirectory\":\"E:\\\\NX\"}\n", Encoding.UTF8);
        var sut = new JsonNxSettingsStore(path);

        NxSettingsLoadResult loaded = await sut.LoadAsync(CancellationToken.None);

        Assert.AreEqual(NxSettingsLoadStatus.UnsupportedSchema, loaded.Status);
        Assert.IsNull(loaded.Settings.NxRootDirectory);
    }

    [TestMethod]
    public async Task SaveAsync_ReplacementFailure_PreservesTargetAndOnlyCleansOwnedTemp()
    {
        string path = SettingsPath();
        string directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        byte[] original = Encoding.UTF8.GetBytes("old settings");
        await File.WriteAllBytesAsync(path, original);
        string unrelatedTemp = Path.Combine(directory, ".settings.json.keep.tmp");
        await File.WriteAllTextAsync(unrelatedTemp, "preserve");
        var sut = new JsonNxSettingsStore(path, (_, _) => throw new IOException("replacement failed"));

        await Assert.ThrowsExactlyAsync<IOException>(
            () => sut.SaveAsync(new NxSettings(1, @"E:\NX"), CancellationToken.None));

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(path));
        Assert.IsTrue(File.Exists(unrelatedTemp));
        Assert.IsEmpty(Directory.GetFiles(directory, ".settings.json.*.tmp")
            .Where(file => !string.Equals(file, unrelatedTemp, StringComparison.OrdinalIgnoreCase))
            .ToArray());
    }

    [TestMethod]
    public async Task SaveAsync_CancelledBeforeWrite_LeavesNoSettingsFile()
    {
        string path = SettingsPath();
        var sut = new JsonNxSettingsStore(path);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await sut.SaveAsync(new NxSettings(1, @"E:\NX"), cancellation.Token);
            Assert.Fail("预先取消的保存必须传播取消。");
        }
        catch (OperationCanceledException)
        {
        }

        Assert.IsFalse(File.Exists(path));
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(path)!)
            && Directory.GetFiles(Path.GetDirectoryName(path)!, ".settings.json.*.tmp").Length > 0);
    }

    private string SettingsPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "nxpdf-settings-" + Guid.NewGuid().ToString("N"));
        scratchDirectories.Add(directory);
        return Path.Combine(directory, "LocalAppData", "NxDrawingPdfExporter", "settings.json");
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("{\"schemaVersion\":\"1\"}")]
    public async Task LoadAsync_WrongJsonShape_IsInvalidWithoutChangingFile(string json)
    {
        string path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, json);
        var loaded = await new JsonNxSettingsStore(path).LoadAsync(CancellationToken.None);
        Assert.AreEqual(NxSettingsLoadStatus.InvalidJson, loaded.Status);
        Assert.AreEqual(json, await File.ReadAllTextAsync(path));
    }
}
