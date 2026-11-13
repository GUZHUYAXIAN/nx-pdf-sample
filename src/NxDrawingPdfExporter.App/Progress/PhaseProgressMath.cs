namespace NxDrawingPdfExporter.App.Progress;

internal readonly record struct PhaseSegments(double Completed, double Active, double Remaining);

internal static class PhaseProgressMath
{
    internal static PhaseSegments Segments(int total, int processed, bool active)
    {
        if (total <= 0)
            return new(0, 0, 1);
        var finished = Math.Clamp(processed, 0, total);
        var running = active && finished < total ? 1 : 0;
        return new(finished / (double)total, running / (double)total, (total - finished - running) / (double)total);
    }

    internal static double Percent(int total, int processed) => total <= 0 ? 0 :
        processed >= total ? 100 : Math.Min(99.99, Math.Round(100d * Math.Max(0, processed) / total, 2));
}
