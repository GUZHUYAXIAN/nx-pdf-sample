using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Contracts.Tests;

[TestClass]
public sealed class WorkerProgressTests
{
    [TestMethod]
    public async Task AtomicSnapshotReadsRecoverAfterReplacementAndRejectOversize()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "NxDrawingPdfExporter.slnx")))
            root = root.Parent ?? throw new DirectoryNotFoundException();
        var directory = Directory.CreateDirectory(Path.Combine(root.FullName, "artifacts", "progress-tests", Guid.NewGuid().ToString("N")));
        var path = Path.Combine(directory.FullName, WorkerProgressJson.FileName);
        try
        {
            WorkerProgressSnapshot Snapshot(int sequence) =>
                new() { RunId = "run", Total = 5, Sequence = sequence, Stage = WorkerProgressStage.Ready };
            WorkerProgressJson.WriteAtomic(Snapshot(1), path);
            Assert.ThrowsExactly<ProtocolException>(() => WorkerProgressJson.ReadBounded(path, 4));

            using var previous = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var writer = Task.Run(() =>
            {
                for (var sequence = 2; sequence <= 24; sequence++)
                    WorkerProgressJson.WriteAtomic(Snapshot(sequence), path);
            });
            try
            {
                for (var index = 0; index < 24; index++)
                {
                    WorkerProgressSnapshot snapshot;
                    try { snapshot = WorkerProgressJson.ReadBounded(path); }
                    // Windows can briefly reject new opens during replacement.
                    catch (IOException) { continue; }
                    Assert.IsTrue(WorkerProgressJson.ValidateForRun(snapshot, "run", 5, 0));
                }
            }
            finally { await writer; }
            using var original = new StreamReader(previous);
            Assert.AreEqual(1L, WorkerProgressJson.Deserialize(original.ReadToEnd()).Sequence);
            Assert.AreEqual(24L, WorkerProgressJson.ReadBounded(path).Sequence);
        }
        finally { directory.Delete(recursive: true); }
    }

    [TestMethod]
    public void StrictRoundTripAndIdentityValidation()
    {
        var value = new WorkerProgressSnapshot { RunId = "run", Total = 5, Sequence = 6, Processed = 2,
            Generated = 1, Failed = 1, CurrentItemIndex = 2, Stage = WorkerProgressStage.Opening };
        var json = WorkerProgressJson.Serialize(value);
        var decoded = WorkerProgressJson.Deserialize(json);
        Assert.IsTrue(WorkerProgressJson.ValidateForRun(decoded, "run", 5, 3));
        Assert.IsFalse(WorkerProgressJson.ValidateForRun(decoded, "run", 5, 6));
        Assert.IsFalse(WorkerProgressJson.ValidateForRun(decoded, "other", 5, 0));
        Assert.IsFalse(WorkerProgressJson.ValidateForRun(decoded, "run", 6, 0));
        decoded.Generated = 2;
        Assert.IsFalse(WorkerProgressJson.ValidateForRun(decoded, "run", 5, 0));
        decoded.Generated = 1;
        decoded.CurrentItemIndex = 3;
        Assert.IsFalse(WorkerProgressJson.ValidateForRun(decoded, "run", 5, 0));
        decoded.CurrentItemIndex = 2;
        decoded.Stage = (WorkerProgressStage)100;
        Assert.IsFalse(WorkerProgressJson.ValidateForRun(decoded, "run", 5, 0));
        Assert.ThrowsExactly<ProtocolException>(() => WorkerProgressJson.Deserialize("{}"));
    }
}
