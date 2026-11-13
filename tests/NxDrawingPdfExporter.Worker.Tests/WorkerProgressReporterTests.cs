using Microsoft.VisualStudio.TestTools.UnitTesting;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Worker;

namespace NxDrawingPdfExporter.Worker.Tests;

[TestClass]
public sealed class WorkerProgressReporterTests
{
    [TestMethod]
    public void WriteFailureDoesNotLoseCumulativeCountsOrAlterTerminalResults()
    {
        var writes = new List<WorkerProgressSnapshot>();
        var attempts = 0;
        var request = new JobRequest { RunId = "fixture", Items = [new(), new(), new(), new()] };
        var reporter = new WorkerProgressReporter(request, Path.GetFullPath("job.json"), (value, _) =>
        {
            if (++attempts == 2)
                throw new IOException("synthetic observer failure");
            writes.Add(WorkerProgressJson.Deserialize(WorkerProgressJson.Serialize(value)));
        });
        reporter.Step(WorkerProgressStage.Initializing);
        reporter.CompleteItem(new FileResult { Status = FileResultStatus.Success });
        reporter.CompleteItem(new FileResult { Status = FileResultStatus.Failed });
        reporter.CompleteItem(new FileResult { Status = FileResultStatus.PureModel });
        reporter.CompleteItem(new FileResult { Status = FileResultStatus.NoValidSheets });
        reporter.Finish(false);
        var final = writes.Last();
        Assert.AreEqual(4, final.Processed);
        Assert.AreEqual(1, final.Generated);
        Assert.AreEqual(1, final.Failed);
        Assert.AreEqual(1, final.PureModel);
        Assert.AreEqual(1, final.NoValidSheets);
        Assert.IsTrue(WorkerProgressJson.ValidateForRun(final, request.RunId, 4, 0));
    }
}
