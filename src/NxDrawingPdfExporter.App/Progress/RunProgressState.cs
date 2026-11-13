using NxDrawingPdfExporter.Contracts;

namespace NxDrawingPdfExporter.App.Progress;

public enum RunPhase { Idle, Preparing, Initializing, Exporting, CheckingWorkerResult, Publishing, Finished, Cancelled, Failed }

public sealed record ExportPhaseCounts(int Total = 0, int Processed = 0, int Generated = 0, int Failed = 0,
    int PureModel = 0, int NoValidSheets = 0, int OtherProcessed = 0);

public sealed record PublicationPhaseCounts(int Total = 0, int Processed = 0, int Saved = 0, int Failed = 0, int Overwritten = 0);

public sealed record RunProgressState
{
    public string RunId { get; init; } = "";
    public long Generation { get; init; }
    public RunPhase Phase { get; init; }
    public ExportPhaseCounts Export { get; init; } = new();
    public PublicationPhaseCounts Publication { get; init; } = new();
    public int? CurrentItemIndex { get; init; }
    public string? CurrentSourcePath { get; init; }
    public WorkerProgressStage? Step { get; init; }
    public bool CancelRequested { get; init; }
    public bool FeedbackUnavailable { get; init; }
    public int Skipped { get; init; }
    public bool PublicationReached { get; init; }
    public TimeSpan Elapsed { get; init; }
}
