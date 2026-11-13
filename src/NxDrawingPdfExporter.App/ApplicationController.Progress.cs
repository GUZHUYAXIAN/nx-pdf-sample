using System.Diagnostics;
using NxDrawingPdfExporter.App.Progress;
using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App;

public sealed partial class ApplicationController
{
    private readonly object progressGate = new();
    private RunProgressState currentProgress = new();
    private long runGeneration;
    private Stopwatch? runClock;
    private volatile bool publicationCancellation;

    public RunProgressState Progress => Volatile.Read(ref currentProgress);
    public event Action? ProgressChanged;

    private void UpdateProgress(Func<RunProgressState, RunProgressState> update)
    {
        lock (progressGate)
        {
            var next = update(currentProgress) with { Elapsed = runClock?.Elapsed ?? TimeSpan.Zero };
            Volatile.Write(ref currentProgress, next);
        }
        ProgressChanged?.Invoke();
    }

    private void AcceptExportProgress(WorkerProgressSnapshot snapshot, long generation, JobRequest request)
    {
        UpdateProgress(state =>
        {
            if (state.Generation != generation || state.RunId != snapshot.RunId
                || state.Phase is not (RunPhase.Initializing or RunPhase.Exporting))
                return state;

            var active = snapshot.CurrentItemIndex;
            return state with
            {
                Phase = snapshot.Stage == WorkerProgressStage.Initializing ? RunPhase.Initializing : RunPhase.Exporting,
                Step = snapshot.Stage,
                CurrentItemIndex = active,
                CurrentSourcePath = active is int index ? request.Items[index].SourcePath : null,
                Export = new(snapshot.Total, snapshot.Processed, snapshot.Generated, snapshot.Failed, snapshot.PureModel,
                    snapshot.NoValidSheets, snapshot.OtherProcessed)
            };
        });
    }

    private void ConfirmExportCounts(JobResult result, JobRequest request, Logging.RunLog runLog)
    {
        if (result.FatalError is not null)
            return;
        var handled = result.Files.Where(file => file.Status != FileResultStatus.Cancelled).ToArray();
        var generated = handled.Count(file => file.Status == FileResultStatus.Success);
        var failures = handled.Count(file => file.Status == FileResultStatus.Failed);
        var models = handled.Count(file => file.Status == FileResultStatus.PureModel);
        var noSheets = handled.Count(file => file.Status == FileResultStatus.NoValidSheets);
        var counts = new ExportPhaseCounts(request.Items.Length, handled.Length, generated, failures, models,
            noSheets, handled.Length - generated - failures - models - noSheets);
        if (Progress.Step == WorkerProgressStage.Finished && Progress.Export != counts)
        {
            try { runLog.Write("Terminal advisory counters differ from the verified Worker result."); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Debug.WriteLine(error); }
        }
        UpdateProgress(state => state with { Export = counts });
    }
}
