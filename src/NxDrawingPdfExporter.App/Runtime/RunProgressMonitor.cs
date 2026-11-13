using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App.Runtime;

internal sealed class RunProgressMonitor : IDisposable, IAsyncDisposable
{
    private readonly string path;
    private readonly string runId;
    private readonly int total;
    private readonly Action<WorkerProgressSnapshot> receive;
    private readonly Action<string?> reportIssue;
    private readonly Func<string, WorkerProgressSnapshot> read;
    private readonly CancellationTokenSource stop = new();
    private WorkerProgressSnapshot? last;
    private Task? pump;
    private int readFailures;
    private bool unavailable;

    internal RunProgressMonitor(string directory, string runId, int total,
        Action<WorkerProgressSnapshot> receive, Action<string?> reportIssue,
        Func<string, WorkerProgressSnapshot>? read = null)
    {
        path = Path.Combine(directory, WorkerProgressJson.FileName);
        this.runId = runId;
        this.total = total;
        this.receive = receive;
        this.reportIssue = reportIssue;
        this.read = read ?? (file => WorkerProgressJson.ReadBounded(file));
    }

    internal Task StartAsync()
    {
        return pump ??= PollAsync();
    }

    internal async Task StopAsync()
    {
        stop.Cancel();
        if (pump is not null)
            await pump;
    }

    private async Task PollAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try
        {
            do
            {
                await ReadOnceAsync(stop.Token);
            } while (await timer.WaitForNextTickAsync(stop.Token));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }

    internal Task ReadFinalAsync() => ReadOnceAsync(default, afterStop: true);

    internal async Task ReadOnceAsync(CancellationToken token, bool afterStop = false)
    {
        WorkerProgressSnapshot value;
        try { value = await Task.Run(() => read(path), token); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ProtocolException)
        {
            if (!token.IsCancellationRequested)
                MarkIssue(error.GetType().Name);
            return;
        }

        if (token.IsCancellationRequested)
            return;
        if (Accept(value, afterStop) || last is { } previous && SameSnapshot(previous, value))
        {
            readFailures = 0;
            if (unavailable)
            {
                unavailable = false;
                reportIssue(null);
            }
        }
        else
            MarkIssue("Invalid or regressive advisory snapshot");
    }

    internal bool Accept(WorkerProgressSnapshot value, bool afterStop = false)
    {
        if ((!afterStop && stop.IsCancellationRequested) || !WorkerProgressJson.ValidateForRun(value, runId, total, last?.Sequence ?? 0))
            return false;
        if (last is { } previous && (value.Processed < previous.Processed || value.Generated < previous.Generated
            || value.Failed < previous.Failed || value.PureModel < previous.PureModel || value.NoValidSheets < previous.NoValidSheets
            || value.OtherProcessed < previous.OtherProcessed || previous.Stage is WorkerProgressStage.Finished or WorkerProgressStage.Faulted
            || value.Processed == previous.Processed && value.Stage < previous.Stage && previous.Stage != WorkerProgressStage.BetweenItems))
            return false;
        last = value;
        receive(value);
        return true;
    }

    private static bool SameSnapshot(WorkerProgressSnapshot left, WorkerProgressSnapshot right) =>
        left.Sequence == right.Sequence && left.RunId == right.RunId && left.SchemaVersion == right.SchemaVersion
        && left.Stage == right.Stage && left.Total == right.Total && left.Processed == right.Processed
        && left.Generated == right.Generated && left.Failed == right.Failed && left.PureModel == right.PureModel
        && left.NoValidSheets == right.NoValidSheets && left.OtherProcessed == right.OtherProcessed
        && left.CurrentItemIndex == right.CurrentItemIndex && left.UpdatedUtc == right.UpdatedUtc;

    private void MarkIssue(string issue)
    {
        if (++readFailures >= 4 && !unavailable)
        {
            unavailable = true;
            reportIssue(issue);
        }
    }

    public void Dispose()
    {
        stop.Cancel();
        stop.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        Dispose();
    }
}
