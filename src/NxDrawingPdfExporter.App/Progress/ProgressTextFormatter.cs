using NxDrawingPdfExporter.App.Localization;

namespace NxDrawingPdfExporter.App.Progress;

internal sealed record ProgressPresentation(string Title, string Counts, string Detail, string ExportSummary,
    PhaseSegments Segments);

internal static class ProgressTextFormatter
{
    internal static ProgressPresentation Format(RunProgressState state, UiText ui)
    {
        var export = state.Export;
        var save = state.Publication;
        var saving = state.PublicationReached;
        var total = saving ? save.Total : export.Total;
        var processed = saving ? save.Processed : export.Processed;
        var active = state.CurrentItemIndex.HasValue;
        var exportCounts = export.Total == 0 ? ui.Get("PhaseNoExport", state.Skipped) :
            ui.Get("PhaseExportCounts", export.Processed, export.Total, PhaseProgressMath.Percent(export.Total, export.Processed), export.Generated, state.Skipped);
        var counts = saving ? save.Total == 0 ? ui.Get("PhaseNoSave") :
            ui.Get("PhaseSaveCounts", save.Processed, save.Total, PhaseProgressMath.Percent(save.Total, save.Processed),
                save.Saved, save.Overwritten, save.Failed) : exportCounts;
        var step = state.Phase switch
        {
            RunPhase.Publishing => ui.Get("PhaseSaveStep"),
            RunPhase.CheckingWorkerResult => ui.Get("PhaseChecking"),
            _ => state.Step is { } workerStep ? ui.Get("Step" + workerStep) : ui.Get("PhaseWaiting")
        };
        var detail = active && state.CurrentSourcePath is { } path
            ? ui.Get("PhaseCurrent", state.CurrentItemIndex!.Value + 1, total, Path.GetFileName(path), step)
            : state.Phase is RunPhase.Initializing or RunPhase.Exporting or RunPhase.CheckingWorkerResult ? step : "";
        if (state.FeedbackUnavailable && state.Phase is RunPhase.Initializing or RunPhase.Exporting)
            detail = string.IsNullOrEmpty(detail) ? ui.Get("PhaseFeedback") : detail + " · " + ui.Get("PhaseFeedback");
        return new(ui.Get(saving ? "PhaseSave" : "PhaseExport"), counts, detail,
            saving ? ui.Get("PhaseExport") + " · " + exportCounts : "", PhaseProgressMath.Segments(total, processed, active));
    }
}
