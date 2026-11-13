using NxDrawingPdfExporter.App.Progress;

namespace NxDrawingPdfExporter.App;

public sealed partial class MainForm
{
    private long elapsedGeneration;
    private TimeSpan confirmedElapsed;
    private long elapsedObservedAt;

    private void UpdateRunProgress()
    {
        var state = controller.Progress;
        if (state.Generation != elapsedGeneration || state.Elapsed != confirmedElapsed)
        {
            elapsedGeneration = state.Generation;
            confirmedElapsed = state.Elapsed;
            elapsedObservedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        cancel.Enabled = controller.IsBusy && !state.CancelRequested;
        cancel.Text = controller.IsBusy && state.CancelRequested ? Ui.Get("PhaseCancelRequested") : Ui.Get("Cancel");

        var shown = state.Generation > 0;
        var presentation = ProgressTextFormatter.Format(state, Ui);
        var title = controller.IsBusy && state.Phase is not RunPhase.Preparing ? presentation.Title : controller.ProgressText;
        if (closePending)
            title = Ui.Get("PendingClose");
        else if (controller.IsBusy && state.CancelRequested && state.Phase is not RunPhase.Preparing)
            title += " · " + Ui.Get("PhaseCancelRequested");
        progressText.Text = title;
        phaseCounts.Visible = shown;
        phaseCounts.Text = presentation.Counts;
        phaseDetail.Visible = shown && presentation.Detail.Length > 0 && controller.IsBusy;
        var elapsed = confirmedElapsed + (controller.IsBusy ? System.Diagnostics.Stopwatch.GetElapsedTime(elapsedObservedAt) : TimeSpan.Zero);
        phaseDetail.Text = phaseDetail.Visible
            ? presentation.Detail + " · " + Ui.Get("PhaseElapsed", elapsed.ToString(@"hh\:mm\:ss")) : "";
        var maxWidth = Math.Max(180, ClientSize.Width - Dip(48));
        phaseCounts.MaximumSize = new(maxWidth, 0);
        phaseDetail.MaximumSize = new(maxWidth, 0);
        hints.SetToolTip(phaseCounts, presentation.ExportSummary);
        hints.SetToolTip(phaseDetail, state.CurrentSourcePath);
        var legend = Ui.Get("PhaseLegend");
        phaseLegend.Visible = shown;
        phaseLegend.Text = legend;
        progress.SetProgress(presentation.Segments, presentation.Title + " · " + presentation.Counts + " · " + legend);
    }
}
