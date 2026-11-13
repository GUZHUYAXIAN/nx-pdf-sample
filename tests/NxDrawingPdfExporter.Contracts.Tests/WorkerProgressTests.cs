using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Contracts.Tests;

[TestClass]
public sealed class WorkerProgressTests
{
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
