using System;
using System.IO;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.Worker;

internal sealed class WorkerProgressReporter
{
    private readonly WorkerProgressSnapshot snapshot;
    private readonly string path;
    private readonly Action<WorkerProgressSnapshot, string> write;
    private bool diagnosed;

    internal WorkerProgressReporter(JobRequest request, string jobPath, Action<WorkerProgressSnapshot, string>? write = null)
    {
        snapshot = new WorkerProgressSnapshot { RunId = request.RunId, Total = request.Items.Length };
        path = Path.Combine(Path.GetDirectoryName(jobPath)!, WorkerProgressJson.FileName);
        this.write = write ?? WorkerProgressJson.WriteAtomic;
    }

    internal int? CurrentItemIndex => snapshot.CurrentItemIndex;

    internal void Step(WorkerProgressStage stage, int? index = null)
    {
        snapshot.Stage = stage;
        snapshot.CurrentItemIndex = index;
        snapshot.UpdatedUtc = DateTime.UtcNow;
        snapshot.Sequence++;
        try { write(snapshot, path); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ProtocolException
            or System.Runtime.Serialization.SerializationException or ArgumentException or NotSupportedException)
        {
            if (diagnosed)
                return;
            diagnosed = true;
            Console.Error.WriteLine("Advisory progress could not be written: " + error.GetType().Name);
        }
    }

    internal void CompleteItem(FileResult result)
    {
        snapshot.Processed++;
        switch (result.Status)
        {
            case FileResultStatus.Success: snapshot.Generated++; break;
            case FileResultStatus.Failed: snapshot.Failed++; break;
            case FileResultStatus.PureModel: snapshot.PureModel++; break;
            case FileResultStatus.NoValidSheets: snapshot.NoValidSheets++; break;
            default: snapshot.OtherProcessed++; break;
        }
        Step(WorkerProgressStage.BetweenItems);
    }

    internal void Finish(bool faulted) => Step(faulted ? WorkerProgressStage.Faulted : WorkerProgressStage.Finished);
}
